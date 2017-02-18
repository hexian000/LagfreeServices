using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LagfreeServices
{
    partial class LagfreeMem : ServiceBase
    {
        public LagfreeMem()
        {
            InitializeComponent();
        }

        const int CheckInterval = 5000;
        Timer UsageCheckTimer;
        object SafeAsyncLock = new object();
        DateTime NextTrim;
        Task TrimTask = null;
        HashSet<string> IgnoreProcessNames;

        protected override void OnStart(string[] args)
        {
            IgnoreProcessNames = new HashSet<string>() { "Memory Compression", "MsMpEng", "services", "NisSrv", "csrss", "lsass", "smss", "wininit", "winlogon" };
            NextTrim = DateTime.UtcNow;
            UsageCheckTimer = new Timer(UsageCheck, null, CheckInterval, CheckInterval);
        }

        protected override void OnStop()
        {
            if (UsageCheckTimer != null)
            {
                lock(SafeAsyncLock)
                    UsageCheckTimer.Dispose();
                UsageCheckTimer = null;
            }
            IgnoreProcessNames = null;
            GC.Collect();
        }


        #region Monitor

        private void UsageCheck(object state)
        {
            if (TrimTask != null) return;
            if (DateTime.UtcNow < NextTrim) return;
            lock(SafeAsyncLock)
            {
                ComputerInfo ci = new ComputerInfo();
                double availPhy = (double)ci.AvailablePhysicalMemory / ci.TotalPhysicalMemory;
                if (availPhy < 0.25)
                {
                    using (TrimTask = new Task(TrimAllProcesses))
                    {
                        TrimTask.Start();
                        TrimTask.Wait();
                    }
                    NextTrim = DateTime.UtcNow.AddMinutes(15);
                    TrimTask = null;
                }
            }
        }

        private void TrimAllProcesses()
        {
            StringBuilder log = new StringBuilder();
            try
            {
                var procs = Process.GetProcesses();
                foreach (var proc in procs)
                {
                    int pid = proc.Id;
                    if (pid == 0 || pid == 4) continue;
                    string pname = "<unknown>";
                    try
                    {
                        pname = proc.ProcessName;
                        if (IgnoreProcessNames.Contains(pname)) continue;
                        Win32Utils.TrimProcessWorkingSet(proc.SafeHandle);
                        log.AppendLine($"缩减进程工作集成功，进程{pid} \"{pname}\"");
                    }
                    catch (Exception ex)
                    {
                        log.AppendLine($"缩减进程工作集失败，进程{pid} \"{pname}\" {ex.GetType().Name}：{ex.Message}");
                    }
                }
                if (log.Length > 0) WriteLogEntry(1000, log.ToString());
            }
            catch (Exception ex) { WriteLogEntry(2000, $"尝试缩减进程工作集时发生意外错误。{Environment.NewLine}{ex.GetType().Name}：{ex.Message} 调试信息：{ex.StackTrace}", true); }
        }

        #endregion

        void WriteLogEntry(int id, string message, bool error = false) 
            => EventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Information, id);
    }
}
