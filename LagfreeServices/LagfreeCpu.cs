using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace LagfreeServices
{
    partial class LagfreeCpu : ServiceBase
    {
        public LagfreeCpu()
        {
            InitializeComponent();
        }

        const int CheckInterval = 1000;
        PerformanceCounter CpuIdle = null;
        PerformanceCounter RestrainedCount = null;
        IntPtr RestrainedAffinity;
        Timer UsageCheckTimer;
        object SafeAsyncLock = new object();
        float RestrainThreshold;

        protected override void OnStart(string[] args)
        {
            int count = Environment.ProcessorCount;
            try { CpuIdle = new PerformanceCounter("Processor", "% Idle Time", "_Total", true); }
            catch (Exception) { }
            if (count < 1 || CpuIdle == null)
            {
                WriteLogEntry(2001, "无法监控CPU", true);
                ExitCode = 2001;
                Stop();
                return;
            }
            if (count > 31)
            {
                WriteLogEntry(2001, "CPU太多，此服务不支持拥有超过31个逻辑处理器的系统", true);
                ExitCode = 2002;
                Stop();
                return;
            }
            RestrainedAffinity = new IntPtr((1 << count) - 2);
            RestrainThreshold = (float)(100.0 - 90.0 / Environment.ProcessorCount);
            if (Lagfree.MyPid < 0) using (var me = Process.GetCurrentProcess()) Lagfree.MyPid = me.Id;
            Lagfree.SetupCategory();
            RestrainedCount = new PerformanceCounter(Lagfree.CounterCategoryName, Lagfree.CpuRestrainedCounterName, false);
            LastCounts = new SortedDictionary<int, double>();
            Restrained = new Dictionary<int, CpuRestrainedProcess>();
            UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
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
            CpuIdle.NextSample();
            UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
        }

        private void InternalDispose()
        {
            InternalPause();
            CpuIdle?.Dispose();
            CpuIdle = null;
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

        struct CpuRestrainedProcess
        {
            public bool Revert;
            public Process Process;
            public ProcessPriorityClass OriginalPriorityClass;
            public int OriginalAffinity;
        }
        private Dictionary<int, CpuRestrainedProcess> Restrained;
        private SortedDictionary<int, double> LastCounts;
        private DateTime LastCountsTime;

        private void UsageCheck(object state)
        {
            lock (SafeAsyncLock)
            {
                float idle = CpuIdle.NextValue();
                if (idle > 50) RevertAll();
                if (idle > RestrainThreshold) return;
                var CpuTimes = ObtainPerProcessUsage(idle < 5);
                StringBuilder log = new StringBuilder();
                foreach (var i in CpuTimes)
                {
                    int pid = i.Key;
                    double cputime = i.Value;

                    if (cputime < 900) break;
                    if (Restrained.ContainsKey(pid)) continue;

                    Process proc = null;
                    string pname = "<unknown>";
                    CpuRestrainedProcess rproc = new CpuRestrainedProcess() { Revert = false, Process = null };
                    // Restrain process
                    try
                    {
                        proc = Process.GetProcessById(i.Key);
                        pname = proc.ProcessName;
                        rproc.OriginalPriorityClass = proc.PriorityClass;
                        rproc.OriginalAffinity = proc.ProcessorAffinity.ToInt32();
                        if (rproc.OriginalPriorityClass != ProcessPriorityClass.Idle && rproc.OriginalPriorityClass != ProcessPriorityClass.BelowNormal)
                        {
                            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                            proc.ProcessorAffinity = RestrainedAffinity;
                            rproc.Process = proc;
                            rproc.Revert = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLogEntry(2002, $"限制进程失败，进程{pid} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                    }
                    Restrained.Add(pid, rproc);
                    if (rproc.Revert)
                    {
                        log.AppendLine($"已限制进程。 进程{pid} \"{pname}\" 在过去1秒内占用CPU时间{cputime}ms");
                        break;
                    }
                }
                if (Lagfree.Verbose && log.Length > 0) WriteLogEntry(1001, log.ToString());
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
                    if (i.Value.Revert)
                    {
                        if (i.Value.Process.HasExited)
                            i.Value.Process.Dispose();
                        else
                        {
                            pname = i.Value.Process.ProcessName;
                            i.Value.Process.PriorityClass = i.Value.OriginalPriorityClass;
                            i.Value.Process.ProcessorAffinity = new IntPtr(i.Value.OriginalAffinity);
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

        private List<KeyValuePair<int, double>> ObtainPerProcessUsage(bool foregroundBoost = false)
        {
            List<KeyValuePair<int, double>> CpuCounts;
            SortedDictionary<int, double> Counts = new SortedDictionary<int, double>();
            StringBuilder log = new StringBuilder();
            HashSet<int> ForegroundPids = Lagfree.GetForegroundPids();
            Process[] procs = Process.GetProcesses();
            DateTime CountsTime = DateTime.UtcNow;
            if ((LastCountsTime - CountsTime).TotalMilliseconds >= CheckInterval * 2) LastCounts.Clear();
            CpuCounts = new List<KeyValuePair<int, double>>(procs.Length);
            foreach (var proc in procs)
            {
                try
                {
                    int pid = proc.Id;
                    string pname = proc.ProcessName;
                    if (pid == 0 || pid == 4 || pid == Lagfree.MyPid || pid == Lagfree.AgentPid
                        || Lagfree.IgnoredProcessNames.Contains(proc.ProcessName)) continue;
                    // Foreground Boost
                    if (ForegroundPids.Contains(pid) && foregroundBoost)
                    {
                        var rproc = new CpuRestrainedProcess()
                        {
                            OriginalPriorityClass = proc.PriorityClass,
                            OriginalAffinity = proc.ProcessorAffinity.ToInt32(),
                            Process = proc,
                            Revert = true
                        };
                        try
                        {
                            if (rproc.OriginalPriorityClass != ProcessPriorityClass.AboveNormal
                                && rproc.OriginalPriorityClass != ProcessPriorityClass.High
                                && rproc.OriginalPriorityClass != ProcessPriorityClass.RealTime)
                            {
                                proc.PriorityClass = ProcessPriorityClass.AboveNormal;
                                rproc.Process = proc;
                                rproc.Revert = true;
                            }
                            log.AppendLine($"已加速前台进程。 进程{pid} \"{pname}\"");
                        }
                        catch (Exception ex)
                        {
                            WriteLogEntry(2002, $"加速前台进程失败，进程{pid} \"{pname}\"{Environment.NewLine}{ex.GetType().Name}：{ex.Message}", true);
                        }
                        Restrained.Add(pid, rproc);
                    }
                    // Obtain data
                    double ptime = proc.TotalProcessorTime.TotalMilliseconds;
                    Counts.Add(pid, ptime);
                    if (LastCounts.ContainsKey(pid))
                        CpuCounts.Add(new KeyValuePair<int, double>(pid, ptime - LastCounts[pid]));
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (Exception ex) { WriteLogEntry(3000, ex.GetType().Name + ":" + ex.Message + "\n" + ex.StackTrace, true); }
            }
            CpuCounts.Sort(new Comparison<KeyValuePair<int, double>>((x, y) => Math.Sign((y.Value - x.Value))));
            LastCounts = Counts;
            LastCountsTime = CountsTime;
            return CpuCounts;
        }
        void WriteLogEntry(int id, string message, bool error = false)
            => EventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Information, id);
        #endregion
    }
}
