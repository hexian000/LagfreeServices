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
            CheckInstallation();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = Busy;
        }

        bool Busy = false;

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            InstallButton.IsEnabled = false;
            Effect = new BlurEffect();
            Busy = true;
            new Task(() =>
            {
                try
                {
                    if (PerformUpdate) LagfreeServicesInstall.PerformUninstall();
                    if (LagfreeServicesInstall.PerformInstall())
                        Dispatcher.InvokeAsync(() => MessageBox.Show("安装完成", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Information));
                    else
                        Dispatcher.InvokeAsync(() => MessageBox.Show("安装完成，但安装过程中发生了错误", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Exclamation));
                }
                finally
                {
                    Busy = false;
                    Dispatcher.InvokeAsync(() =>
                    {
                        Cursor = Cursors.Arrow;
                        Effect = null;
                        CheckInstallation();
                    });
                }
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
                try
                {
                    if (LagfreeServicesInstall.PerformUninstall())
                        Dispatcher.InvokeAsync(() => MessageBox.Show("卸载完成", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Information));
                    else
                        Dispatcher.InvokeAsync(() => MessageBox.Show("卸载完成，但卸载过程中发生了错误", "Lagfree Services", MessageBoxButton.OK, MessageBoxImage.Exclamation));
                }
                finally
                {
                    Busy = false;
                    Dispatcher.InvokeAsync(() =>
                    {
                        Cursor = Cursors.Arrow;
                        Effect = null;
                        CheckInstallation();
                    });
                }
            }).Start();
        }
    }
}
