using LagfreeAgent;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

namespace LagfreeInstaller
{
    class LagfreeServicesInstall
    {
        private class InstallFile
        {
            public InstallFile(string source, string target) { Source = source; Target = target; }
            public string Source, Target;
        }

        private static List<InstallFile> InstallFiles = new List<InstallFile> {
                    new InstallFile( "LagfreeServices.ex_", "LagfreeServices.exe" ),
                    new InstallFile( "LagfreeAgent.exe", "LagfreeAgent.exe" )
                };


        internal static bool CheckInstallSource()
        {
            bool SourceExist = true;
            foreach (var i in InstallFiles) if (!File.Exists(Path.Combine(App.SourceDir, i.Source)))
                {
                    SourceExist = false;
                    break;
                }
            return SourceExist;
        }

        internal static bool PerformInstall()
        {
            bool success = true;
            try
            {
                ShutdownAgent();
                CopyFiles(App.SourceDir, App.TargetDir);
                using (var proc = Process.Start(App.ExePath, "install"))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode == 1) success = false;
                }
                string AgentPath = Path.Combine(App.TargetDir, "LagfreeAgent.exe");
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    hklm.SetValue("Lagfree Desktop Agent", "\"" + AgentPath + "\"");
                Process.Start(new ProcessStartInfo(AgentPath) { WorkingDirectory = App.TargetDir });
            }
            catch { success = false; }
            return success;
        }

        internal static bool PerformUninstall()
        {
            bool success = true;
            try
            {
                using (var proc = Process.Start(App.ExePath, "uninstall"))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode == 1) success = false;
                }
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    hklm.DeleteValue("Lagfree Desktop Agent");
                ShutdownAgent();
                DeleteFiles(App.TargetDir);
            }
            catch { success = false; }
            return success;
        }

        private static void ShutdownAgent()
        {
            try
            {
                IpcClientChannel AgentChannel;
                VisiblePids ForegroundPids = null;
                AgentChannel = new IpcClientChannel();
                ChannelServices.RegisterChannel(AgentChannel, true);
                RemotingConfiguration.RegisterWellKnownClientType(typeof(VisiblePids), "ipc://LagfreeAgent/VisiblePids");
                ForegroundPids = new VisiblePids();
                int pid = ForegroundPids.GetPid();
                using (var proc = Process.GetProcessById(pid))
                {
                    ForegroundPids.Exit();
                    proc.WaitForExit();
                }
            }
            catch (Exception) { }
        }

        private static void CopyFiles(string SourceDir, string TargetDir)
        {
            Directory.CreateDirectory(TargetDir);
            foreach (var i in InstallFiles)
                File.Copy(Path.Combine(SourceDir, i.Source), Path.Combine(TargetDir, i.Target), true);
        }

        private static void DeleteFiles(string TargetDir)
        {
            Directory.Delete(TargetDir, true);
        }
    }
}
