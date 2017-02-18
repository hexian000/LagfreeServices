using Microsoft.Win32;
using System;
using System.Collections;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.Windows;

namespace LagfreeInstaller
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        internal static string SourceDir, TargetDir, ExePath;

        protected override void OnStartup(StartupEventArgs e)
        {
            using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                object versionCode = 0;
                if (ndpKey != null) versionCode = ndpKey.GetValue("Release", 0);
                if (versionCode is int && (int)versionCode < 394802)
                {
                    MessageBox.Show("安装失败：安装此程序前，您需要先安装.NET Framework 4.6.2", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(2);
                }
            }

            Assembly me = Assembly.GetExecutingAssembly();
            ExePath = me.Location;
            SourceDir = Path.GetDirectoryName(ExePath);
            TargetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LagfreeServices");
            if (e.Args.Length == 1)
            {
                if (e.Args[0] == "install")
                {
                    try { Install(TargetDir); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("安装失败：" + ex.Message, "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Error);
                        Environment.Exit(1);
                    }
                }
                else if (e.Args[0] == "uninstall")
                {
                    try { Uninstall(TargetDir); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("卸载失败：" + ex.Message, "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Error);
                        Environment.Exit(1);
                    }
                }
                Environment.Exit(0);
            }
            base.OnStartup(e);
        }

        private void Install(string TargetDir)
        {
            using (var AsmInst = new AssemblyInstaller(Path.Combine(TargetDir, "LagfreeServices.exe"), new string[] { "/LogFile=" }) { UseNewContext = true })
            {
                Hashtable InsState = new Hashtable();
                AsmInst.Install(InsState);
                AsmInst.Commit(InsState);
            }
        }

        private void Uninstall(string TargetDir)
        {
            using (var AsmInst = new AssemblyInstaller(Path.Combine(TargetDir, "LagfreeServices.exe"), new string[] { "/LogFile=" }))
            {
                Hashtable InsState = new Hashtable();
                AsmInst.Uninstall(InsState);
            }
        }
    }
}
