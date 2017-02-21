using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
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
            EnableUI = () =>
            {
                Cursor = Cursors.Arrow;
                Effect = null;
                CheckInstallation();
                Busy = false;
            };
            DisableUI = () =>
            {
                Cursor = Cursors.Wait;
                InstallButton.IsEnabled = false;
                UninstallButton.IsEnabled = false;
                Effect = new BlurEffect();
                Busy = true;
            };
            SuccessMsg = () => MessageBox.Show("操作完成", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Information);
            FailedMsg = () => MessageBox.Show("操作完成，但过程中发生了错误", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private bool PerformUpdate = false;
        private void CheckInstallation()
        {
            bool CanInst = true, CanUnins = false;
            CanInst = LagfreeServicesInstall.CheckInstallSource();
            CanUnins = Directory.Exists(App.TargetDir);
            if (CanInst && CanUnins) { InstallButton.Content = "更新"; PerformUpdate = true; }
            else { InstallButton.Content = "安装"; PerformUpdate = false; }
            InstallButton.IsEnabled = CanInst;
            UninstallButton.IsEnabled = CanUnins;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                while (App.ProgramStarted != null)
                {
                    if (!App.ProgramStarted.WaitOne(1000)) continue;
                    Dispatcher.InvokeAsync(() => Activate());
                    App.ProgramStarted.Reset();
                }
            }).Start();
            EnableUI();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = Busy;
        }

        bool Busy = false;
        private Action EnableUI, DisableUI;
        private Action SuccessMsg, FailedMsg;
        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            DisableUI();
            new Task(() =>
            {
                try
                {
                    if (PerformUpdate) LagfreeServicesInstall.PerformUninstall();
                    if (LagfreeServicesInstall.PerformInstall())
                        Dispatcher.InvokeAsync(SuccessMsg);
                    else
                        Dispatcher.InvokeAsync(FailedMsg);
                }
                finally { Dispatcher.InvokeAsync(EnableUI); }
            }).Start();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            DisableUI();
            new Task(() =>
            {
                try
                {
                    if (LagfreeServicesInstall.PerformUninstall())
                        Dispatcher.InvokeAsync(SuccessMsg);
                    else
                        Dispatcher.InvokeAsync(FailedMsg);
                }
                finally { Dispatcher.InvokeAsync(EnableUI); }
            }).Start();
        }
    }
}
