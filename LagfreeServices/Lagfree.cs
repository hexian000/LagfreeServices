using LagfreeAgent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

namespace LagfreeServices
{
    internal class Lagfree
    {
        internal const bool Verbose = false;
        internal static HashSet<string> IgnoredProcessNames = new HashSet<string>() { "Memory Compression", "MsMpEng", "services", "NisSrv", "csrss", "lsass", "smss", "wininit", "winlogon" };
        internal static int MyPid = -1;

        internal const string CounterCategoryName = "Lagfree Services";
        internal const string CpuRestrainedCounterName = "CPU Restrained Process Count";
        internal const string IoRestrainedCounterName = "IO Restrained Process Count";

        private static object PerfCounterSyncLock = new object();
        internal static bool SetupCategory()
        {
            lock (PerfCounterSyncLock)
            {
                if (!PerformanceCounterCategory.Exists(CounterCategoryName))
                {

                    CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

                    // Add the counters.
                    CounterCreationData ioRestrainCount64 = new CounterCreationData()
                    {
                        CounterType = PerformanceCounterType.NumberOfItems64,
                        CounterName = IoRestrainedCounterName,
                        CounterHelp = "指示Lagfree HDD服务正限制的进程数量"
                    };
                    counterDataCollection.Add(ioRestrainCount64);


                    CounterCreationData cpuRestrainCount64 = new CounterCreationData()
                    {
                        CounterType = PerformanceCounterType.NumberOfItems64,
                        CounterName = CpuRestrainedCounterName,
                        CounterHelp = "指示Lagfree CPU服务正限制的进程数量"
                    };
                    counterDataCollection.Add(cpuRestrainCount64);

                    // Create the category.
                    PerformanceCounterCategory.Create(CounterCategoryName,
                        "指示Lagfree Services工作状态的性能指标。",
                        PerformanceCounterCategoryType.SingleInstance, counterDataCollection);

                    return true;
                }
                else return false;
            }
        }

        private static IpcClientChannel AgentChannel;
        private static VisiblePids ForegroundPids = null;
        private static DateTime CacheAlive = DateTime.FromBinary(0);
        private static HashSet<int> VisiblePidsCache;
        private static object IpcSyncLock = new object();
        internal static HashSet<int> GetForegroundPids()
        {
            lock (IpcSyncLock)
            {
                if (ForegroundPids == null) InitializeIpc();
                DateTime Call = DateTime.UtcNow;
                if (Call > CacheAlive)
                {
                    try { VisiblePidsCache = ForegroundPids.Get(); }
                    catch
                    {
                        AgentPidCache = -1;
                        VisiblePidsCache = new HashSet<int>();
                    }
                }
                CacheAlive = Call.AddSeconds(2);
                return VisiblePidsCache;
            }
        }

        private static int AgentPidCache = -1;
        internal static int AgentPid
        {
            get
            {
                lock (IpcSyncLock)
                {
                    if (ForegroundPids == null) InitializeIpc();
                    if (AgentPidCache == -1)
                        try { AgentPidCache = ForegroundPids.GetPid(); }
                        catch { }
                    return AgentPidCache;
                }
            }
        }

        private static void InitializeIpc()
        {
            AgentChannel = ChannelServices.GetChannel("LagfreeAgentClient") as IpcClientChannel;
            if (AgentChannel == null)
            {
                AgentChannel = new IpcClientChannel("LagfreeAgentClient", null);
                ChannelServices.RegisterChannel(AgentChannel, true);
            }
            RemotingConfiguration.RegisterWellKnownClientType(typeof(VisiblePids), "ipc://LagfreeAgent/VisiblePids");
            ForegroundPids = new VisiblePids();
        }
    }
}
