using System;
using System.Collections;
using System.Configuration.Install;
using System.Diagnostics;
using System.ServiceProcess;

namespace LagfreeServices
{
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        public override void Install(IDictionary stateSaver)
        {
            if (PerformanceCounterCategory.Exists(Lagfree.CounterCategoryName))
                PerformanceCounterCategory.Delete(Lagfree.CounterCategoryName);
            base.Install(stateSaver);
            StartService(siHddServiceInst.ServiceName);
            StartService(siCpuServiceInst.ServiceName);
            StartService(siMemServiceInst.ServiceName);
        }

        public override void Uninstall(IDictionary savedState)
        {
            try
            {
                StopService(siMemServiceInst.ServiceName);
                StopService(siCpuServiceInst.ServiceName);
                StopService(siHddServiceInst.ServiceName);
            }
            catch { foreach (var srv in Process.GetProcessesByName("LagfreeServices")) using (srv) srv.Kill(); }
            base.Uninstall(savedState);
            if (PerformanceCounterCategory.Exists(Lagfree.CounterCategoryName))
                PerformanceCounterCategory.Delete(Lagfree.CounterCategoryName);
        }

        private static TimeSpan ServiceTimeout = TimeSpan.FromSeconds(30);

        private void StartService(string name)
        {
            using (var SrvCtl = new ServiceController(name))
            {
                switch (SrvCtl.Status)
                {
                    case ServiceControllerStatus.ContinuePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Paused:
                        SrvCtl.Continue();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.PausePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Paused, ServiceTimeout);
                        SrvCtl.Continue();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Running: break;
                    case ServiceControllerStatus.StartPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Stopped:
                        SrvCtl.Start();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.StopPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        SrvCtl.Start();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        break;
                }
                if (SrvCtl.Status != ServiceControllerStatus.Running) throw new Exception("启动服务" + SrvCtl.ServiceName + "失败");
            }
        }

        private void StopService(string name)
        {
            using (var SrvCtl = new ServiceController(name))
            {
                switch (SrvCtl.Status)
                {
                    case ServiceControllerStatus.ContinuePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Paused:
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.PausePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Paused, ServiceTimeout);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Running:
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.StartPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                    case ServiceControllerStatus.Stopped:
                        break;
                    case ServiceControllerStatus.StopPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                        break;
                }
                if (SrvCtl.Status != ServiceControllerStatus.Stopped) throw new Exception("停止服务" + SrvCtl.ServiceName + "失败");
            }
        }
    }
}
