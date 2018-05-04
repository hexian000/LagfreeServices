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
        float RestrainThreshold = 10, IdleThreshold = 50;

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
                    try
                    {
                        if (!HasNominalMediaRotationRate(int.Parse(inst.Split(' ')[0])))
                            WriteLogEntry(3001, "注意：检测到OS安装在非旋转存储设备上");
                    }
                    catch (Win32Exception)
                    {
                        WriteLogEntry(3002, "警告：无法检测到OS安装所在存储设备的旋转速度", true);
                    }
                    Disk = new PerformanceCounter(PhysicalDiskCategoryName, "% Idle Time", inst, true);
                    Disk.NextValue();
                    break;
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
                Restrained = new Dictionary<int, HddRestrainedProcess>();
                LastCounts = new SortedDictionary<int, IO_COUNTERS>();
                UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
            }
        }

        protected override void OnStop()
        {
            InternalPause();
            InternalDispose();
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
            Disk.NextValue();
            UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
        }

        private void InternalDispose()
        {
            InternalPause();
            lock (SafeAsyncLock) Disk?.Dispose();
            Disk = null;
            if (Restrained != null) RevertAll();
            RestrainedCount?.Dispose();
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

        struct HddRestrainedProcess
        {
            public bool Revert;
            public Process Process;
            public int OriginalIoPriority, NewIoPriority;
        }
        private Dictionary<int, HddRestrainedProcess> Restrained;
        private SortedDictionary<int, IO_COUNTERS> LastCounts;
        private DateTime LastCountsTime;

        private void UsageCheck(object state)
        {
            lock (SafeAsyncLock)
            {
                float diskIdle = Disk.NextValue();
                if (diskIdle > IdleThreshold) RevertAll();
                if (diskIdle > RestrainThreshold) return;
                RestrainProcess(ObtainPerProcessUsage());
            }
        }

        private void RestrainProcess(List<KeyValuePair<int, ulong>> IOBytes)
        {
            int RestrainPerSample = 3;
            ulong? prevRw = null;
            StringBuilder log = new StringBuilder();
            foreach (var i in IOBytes)
            {
                int pid = i.Key;
                ulong rw = i.Value;

                if (rw < 512 || Restrained.ContainsKey(pid)) continue;
                if (prevRw != null) { if ((double)rw / prevRw < 0.8) continue; }
                else prevRw = rw;

                Process proc = null;
                string pname = "<unknown>";
                // Restrain process
                HddRestrainedProcess rproc = new HddRestrainedProcess() { Revert = false, Process = null };
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
                        rproc.NewIoPriority = 0;
                    }
                }
                catch (ArgumentException) { }
                catch (Exception ex)
                {
                    WriteLogEntry(2002, $"限制进程失败，进程{pid} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                }
                finally { if (!rproc.Revert) proc?.Dispose(); }
                Restrained.Add(pid, rproc);
                if (rproc.Revert)
                {
                    log.AppendLine($"已限制进程。 进程{pid} \"{pname}\" 在过去{CheckInterval}ms内造成读写压力{rw}");
                    RestrainPerSample--;
                    if (RestrainPerSample <= 0) break;
                }
            }
            if (Lagfree.Verbose && log.Length > 0) WriteLogEntry(1001, log.ToString());
            RestrainedCount.RawValue = Restrained.Count;
        }

        private void RevertAll()
        {
            StringBuilder log = new StringBuilder();
            foreach (var i in Restrained)
            {
                string pname = "<unknown>";
                try
                {
                    if (i.Value.Revert)
                    {
                        if (i.Value.Process.HasExited)
                            i.Value.Process.Dispose();
                        else
                        {
                            pname = i.Value.Process.ProcessName;
                            SafeProcessHandle hProc = i.Value.Process.SafeHandle;
                            if (GetIOPriority(hProc) == i.Value.NewIoPriority) SetIOPriority(hProc, i.Value.OriginalIoPriority);
                            log.AppendLine($"已恢复进程。 进程{i.Key} \"{pname}\"");
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLogEntry(2003, $"恢复进程时发生错误。 进程{i.Key} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                }
            }
            Restrained.Clear();
            RestrainedCount.RawValue = 0;
            if (Lagfree.Verbose && log.Length > 0) WriteLogEntry(1002, log.ToString());
        }

        private List<KeyValuePair<int, ulong>> ObtainPerProcessUsage()
        {
            List<KeyValuePair<int, ulong>> IOBytes;
            SortedDictionary<int, IO_COUNTERS> Counts = new SortedDictionary<int, IO_COUNTERS>();
            HashSet<int> ForegroundPids = Lagfree.GetForegroundPids();
            Process[] procs = Process.GetProcesses();
            DateTime CountsTime = DateTime.UtcNow;
            if ((LastCountsTime - CountsTime).TotalMilliseconds >= CheckInterval * 2) LastCounts.Clear();
            IOBytes = new List<KeyValuePair<int, ulong>>(procs.Length);
			ulong SafeSub(ulong a, ulong b) => a >= b ? a - b : 0;
			foreach (var proc in procs)
            {
                try
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4 || pid == Lagfree.MyPid || pid == Lagfree.AgentPid
                        || ForegroundPids.Contains(pid)
                        || Lagfree.IgnoredProcessNames.Contains(proc.ProcessName)) continue;
                    using (SafeProcessHandle hProcess = proc.SafeHandle)
                    {
                        IO_COUNTERS curr = GetIOCounters(hProcess);
                        Counts.Add(proc.Id, curr);
                        if (LastCounts.ContainsKey(proc.Id))
                        {
                            IO_COUNTERS last = LastCounts[proc.Id];
                            IOBytes.Add(new KeyValuePair<int, ulong>(proc.Id,
                                SafeSub(curr.ReadTransferCount, last.ReadTransferCount) +
                                SafeSub(curr.WriteTransferCount, last.WriteTransferCount) +
                                SafeSub(curr.OtherTransferCount, last.OtherTransferCount) +
                                (
                                SafeSub(curr.ReadOperationCount, last.ReadOperationCount) +
                                SafeSub(curr.WriteOperationCount, last.WriteOperationCount) +
                                SafeSub(curr.OtherOperationCount, last.OtherOperationCount)
                                )
                                ));
                        }
                    }
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (Exception ex) { WriteLogEntry(3000, ex.GetType().Name + ":" + ex.Message + "\n" + ex.StackTrace, true); }
            }
            IOBytes.Sort(new Comparison<KeyValuePair<int, ulong>>((x, y) => (y.Value > x.Value) ? 1 : ((x.Value > y.Value) ? -1 : 0)));
            LastCounts = Counts;
            LastCountsTime = CountsTime;
            return IOBytes;
        }

        private void ForegroundBoost()
        {
            StringBuilder log = new StringBuilder();
            HashSet<int> ForegroundPids = Lagfree.GetForegroundPids();
            Process[] procs = Process.GetProcesses();
            foreach (var proc in procs)
            {
                try
                {
                    int pid = proc.Id;
                    string pname = proc.ProcessName;
                    if (pid == 0 || pid == 4 || pid == Lagfree.MyPid || pid == Lagfree.AgentPid
                        || Lagfree.IgnoredProcessNames.Contains(proc.ProcessName)) continue;
                    // Foreground Boost
                    if (ForegroundPids.Contains(pid))
                    {
                        var rproc = new HddRestrainedProcess()
                        {
                            OriginalIoPriority = GetIOPriority(proc.SafeHandle),
                            Process = proc,
                            Revert = true
                        };
                        try
                        {
                            if (rproc.OriginalIoPriority <= 2)
                            {
                                SetIOPriority(proc.SafeHandle, 3);
                                rproc.Process = proc;
                                rproc.Revert = true;
                                rproc.NewIoPriority = 3;
                            }
                            log.AppendLine($"已加速前台进程。 进程{pid} \"{pname}\"");
                        }
                        catch (Exception ex)
                        {
                            WriteLogEntry(2002, $"加速前台进程失败，进程{pid} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                        }
                        Restrained.Add(pid, rproc);
                    }
                    continue;
                }
                catch { }
            }
            if (log.Length > 0) WriteLogEntry(1003, log.ToString());
        }

        void WriteLogEntry(int id, string message, bool error = false)
            => EventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Information, id);
    }
    #endregion
}
