using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using static LagfreeServices.Win32Utils;

namespace LagfreeServices
{
    public partial class LagfreeHdd : ServiceBase
    {
        public LagfreeHdd()
        {
            InitializeComponent();
        }

        const int CheckInterval = 2000;
        PerformanceCounter Disk = null;
        PerformanceCounter RestrainedCount = null;
        Timer UsageCheckTimer;
        object SafeAsyncLock = new object();

        protected override void OnStart(string[] args)
        {
            if (Lagfree.MyPid < 0) using (var me = Process.GetCurrentProcess()) Lagfree.MyPid = me.Id;
            const string PhysicalDiskCategoryName = "PhysicalDisk";
            var phyDisks = new PerformanceCounterCategory(PhysicalDiskCategoryName);
            var instDisks = phyDisks.GetInstanceNames();
            string SysDrive = Environment.GetFolderPath(Environment.SpecialFolder.System).Substring(0, 2).ToLowerInvariant();
            foreach (var inst in instDisks)
            {
                if (inst == "_Total") continue;
                if (inst.ToLowerInvariant().Contains(SysDrive))
                {
                    bool IsRotational = true;
                    try { IsRotational = HasNominalMediaRotationRate(int.Parse(inst.Split(' ')[0])); }
                    catch (Win32Exception) { }
                    if (IsRotational)
                    {
                        Disk = new PerformanceCounter(PhysicalDiskCategoryName, "% Idle Time", inst, true);
                        Disk.NextSample();
                        break;
                    }
                }
            }
            if (Disk == null)
            {
                WriteLogEntry(2001, "无法监控磁盘", true);
                ExitCode = 2001;
                Stop();
            }
            else
            {
                Lagfree.SetupCategory();
                RestrainedCount = new PerformanceCounter(Lagfree.CounterCategoryName, Lagfree.IoRestrainedCounterName, false);
                Restrained = new Dictionary<int, RestrainedProcess>();
                LastCounts = new SortedDictionary<int, IO_COUNTERS>();
                UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
            }
        }

        protected override void OnStop()
        {
            InternalPause();
            InternalDispose();
            GC.Collect();
        }

        private void InternalPause()
        {
            if (UsageCheckTimer != null)
            {
                lock (SafeAsyncLock)
                    UsageCheckTimer.Dispose();
                UsageCheckTimer = null;
            }
        }

        private void InternalResume()
        {
            Disk.NextSample();
            UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
        }

        private void InternalDispose()
        {
            InternalPause();
            lock (SafeAsyncLock) Disk.Dispose();
            Disk = null;
            RevertAll();
            RestrainedCount.Dispose();
            RestrainedCount = null;
            Restrained = null;
            LastCounts = null;
        }
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.Suspend:
                    InternalPause();
                    break;
                case PowerBroadcastStatus.ResumeSuspend:
                    InternalResume();
                    break;
            }
            return base.OnPowerEvent(powerStatus);
        }

        protected override void OnShutdown()
        {
            InternalPause();
            InternalDispose();
            base.OnShutdown();
        }

        #region Monitor

        struct RestrainedProcess
        {
            public bool Revert;
            public Process Process;
            public int OriginalIoPriority;
        }
        private Dictionary<int, RestrainedProcess> Restrained;
        private SortedDictionary<int, IO_COUNTERS> LastCounts;
        private SortedDictionary<int, Process> LastProcs;

        private void UsageCheck(object state)
        {
            lock (SafeAsyncLock)
            {
                int RestrainPerSample = 3;
                StringBuilder log = new StringBuilder();
                float diskIdle = 100;
                diskIdle = Disk.NextValue();
                if (diskIdle > 50) RevertAll();
                if (diskIdle > 10) return;
                List<KeyValuePair<int, long>> IOBytes = ObtainPerProcessUsage();
                long prevRw = -1;
                foreach (var i in IOBytes)
                {
                    int pid = i.Key;
                    long rw = i.Value;

                    if (rw < 1 || Restrained.ContainsKey(pid)) continue;
                    if (prevRw >= 0) { if ((double)rw / prevRw < 0.8) continue; }
                    else prevRw = rw;

                    Process proc = null;
                    string pname = "<unknown>";
                    // Restrain process
                    RestrainedProcess rproc = new RestrainedProcess() { Revert = false, Process = null };
                    try
                    {
                        proc = Process.GetProcessById(i.Key);
                        pname = proc.ProcessName;
                        SafeProcessHandle hProc = proc.SafeHandle;
                        rproc.OriginalIoPriority = GetIOPriority(hProc);
                        if (rproc.OriginalIoPriority > 0)
                        {
                            SetIOPriority(hProc, 0);
                            rproc.Process = proc;
                            rproc.Revert = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLogEntry(2002, $"限制进程失败，进程{pid} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                    }
                    finally { if (!rproc.Revert) proc?.Dispose(); }
                    Restrained.Add(pid, rproc);
                    if (rproc.Revert)
                    {
                        log.AppendLine($"已限制进程。 进程{pid} \"{pname}\" 在过去2秒内读写{rw}字节");
                        RestrainPerSample--;
                        if (RestrainPerSample <= 0) break;
                    }
                }
                if (log.Length > 0) WriteLogEntry(1001, log.ToString());
                RestrainedCount.RawValue = Restrained.Count;
            }
        }

        private void RevertAll()
        {
            StringBuilder log = new StringBuilder();
            foreach (var i in Restrained)
            {
                string pname = "<unknown>";
                try
                {
                    if (i.Value.Revert) if (!i.Value.Process.HasExited)
                        {
                            pname = i.Value.Process.ProcessName;
                            SafeProcessHandle hProc = i.Value.Process.SafeHandle;
                            SetIOPriority(hProc, i.Value.OriginalIoPriority);
                            log.AppendLine($"已恢复进程。 进程{i.Key} \"{pname}\"");
                        }
                }
                catch (Exception ex)
                {
                    WriteLogEntry(2003, $"恢复进程时发生错误。 进程{i.Key} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                }
                finally { i.Value.Process?.Dispose(); }
            }
            Restrained.Clear();
            RestrainedCount.RawValue = 0;
            if (log.Length > 0) WriteLogEntry(1002, log.ToString());
        }

        private List<KeyValuePair<int, long>> ObtainPerProcessUsage()
        {
            List<KeyValuePair<int, long>> IOBytes;
            SortedDictionary<int, IO_COUNTERS> Counts = new SortedDictionary<int, IO_COUNTERS>();
            HashSet<int> IgnoredPids = Lagfree.GetForegroundPids();
            Process[] procs = Process.GetProcesses();
            IOBytes = new List<KeyValuePair<int, long>>(procs.Length);
            foreach (var proc in procs)
            {
                try
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4 || pid == Lagfree.MyPid || pid == Lagfree.AgentPid
                        || IgnoredPids.Contains(pid)
                        || Lagfree.IgnoredProcessNames.Contains(proc.ProcessName)) continue;
                    using (SafeProcessHandle hProcess = proc.SafeHandle)
                    {
                        IO_COUNTERS curr = GetIOCounters(hProcess);
                        Counts.Add(proc.Id, curr);
                        if (LastCounts.ContainsKey(proc.Id))
                        {
                            IO_COUNTERS last = LastCounts[proc.Id];
                            IOBytes.Add(new KeyValuePair<int, long>(proc.Id,
                                ((long)curr.ReadTransferCount - (long)last.ReadTransferCount) +
                                ((long)curr.WriteTransferCount - (long)last.WriteTransferCount) +
                                ((long)curr.OtherTransferCount - (long)last.OtherTransferCount)
                                ));
                        }
                    }
                }
                catch { }
            }
            IOBytes.Sort(new Comparison<KeyValuePair<int, long>>((x, y) => Math.Sign(y.Value - x.Value)));
            LastCounts = Counts;
            return IOBytes;
        }

        void WriteLogEntry(int id, string message, bool error = false)
            => EventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Information, id);
    }
    #endregion
}
