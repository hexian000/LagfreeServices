using LagfreeAgent;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;

namespace LagfreeInstaller
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (Directory.Exists(App.TargetDir)) InstallButton.IsEnabled = false;
            else
            {
                UninstallButton.IsEnabled = false;
                foreach (var i in InstallFiles) if (!File.Exists(Path.Combine(App.SourceDir, i.Source)))
                    {
                        InstallButton.IsEnabled = false;
                        break;
                    }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = Busy;
            base.OnClosing(e);
        }

        private class InstallFile
        {
            public InstallFile(string source, string target) { Source = source; Target = target; }
            public string Source, Target;
        }

        private static List<InstallFile> InstallFiles = new List<InstallFile> {
                    new InstallFile( "LagfreeServices.ex_", "LagfreeServices.exe" ),
                    new InstallFile( "LagfreeAgent.exe", "LagfreeAgent.exe" )
                };

        bool Busy = false;

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            InstallButton.IsEnabled = false;
            Effect = new BlurEffect();
            Busy = true;
            new Task(() =>
            {
                ShutdownAgent();
                CopyFiles(App.SourceDir, App.TargetDir);
                using (var proc = Process.Start(App.ExePath, "install"))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode == 1)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Cursor = Cursors.Arrow;
                            Effect = null;
                        });
                    }
                }
                string AgentPath = Path.Combine(App.TargetDir, "LagfreeAgent.exe");
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    hklm.SetValue("Lagfree Desktop Agent", "\"" + AgentPath + "\"");
                Process.Start(new ProcessStartInfo(AgentPath) { WorkingDirectory = App.TargetDir });
                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    Effect = null;
                    Busy = false;
                    MessageBox.Show("安装完成", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }).Start();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            UninstallButton.IsEnabled = false;
            Effect = new BlurEffect();
            Busy = true;
            new Task(() =>
            {
                using (var proc = Process.Start(App.ExePath, "uninstall"))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode == 1)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Cursor = Cursors.Arrow;
                            Effect = null;
                        });
                    }
                }
                using (RegistryKey hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    hklm.DeleteValue("Lagfree Desktop Agent");
                ShutdownAgent();
                DeleteFiles(App.TargetDir);
                Dispatcher.Invoke(() =>
                {
                    Cursor = Cursors.Arrow;
                    Effect = null;
                    Busy = false;
                    MessageBox.Show("卸载完成", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }).Start();
        }

        private void ShutdownAgent()
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

        private void CopyFiles(string SourceDir, string TargetDir)
        {
            Directory.CreateDirectory(TargetDir);
            foreach (var i in InstallFiles)
                File.Copy(Path.Combine(SourceDir, i.Source), Path.Combine(TargetDir, i.Target), true);
        }

        private void DeleteFiles(string TargetDir)
        {
            //foreach (var i in InstallFiles)
            //    File.Delete(i.Target);
            Directory.Delete(TargetDir, true);
        }
    }
}
