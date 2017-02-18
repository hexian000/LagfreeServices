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
            StopService(siMemServiceInst.ServiceName);
            StopService(siCpuServiceInst.ServiceName);
            StopService(siHddServiceInst.ServiceName);
            base.Uninstall(savedState);
            if (PerformanceCounterCategory.Exists(Lagfree.CounterCategoryName))
                PerformanceCounterCategory.Delete(Lagfree.CounterCategoryName);
        }

        private void StartService(string name)
        {
            using (var SrvCtl = new ServiceController(name))
            {
                switch (SrvCtl.Status)
                {
                    case ServiceControllerStatus.ContinuePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                    case ServiceControllerStatus.Paused:
                        SrvCtl.Continue();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                    case ServiceControllerStatus.PausePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Paused);
                        SrvCtl.Continue();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                    case ServiceControllerStatus.Running: break;
                    case ServiceControllerStatus.StartPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                    case ServiceControllerStatus.Stopped:
                        SrvCtl.Start();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                    case ServiceControllerStatus.StopPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        SrvCtl.Start();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        break;
                }
            }
        }

        private void StopService(string name)
        {
            using (var SrvCtl = new ServiceController(name))
            {
                switch (SrvCtl.Status)
                {
                    case ServiceControllerStatus.ContinuePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    case ServiceControllerStatus.Paused:
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    case ServiceControllerStatus.PausePending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Paused);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    case ServiceControllerStatus.Running:
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    case ServiceControllerStatus.StartPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Running);
                        SrvCtl.Stop();
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    case ServiceControllerStatus.Stopped:
                        break;
                    case ServiceControllerStatus.StopPending:
                        SrvCtl.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                }
            }
        }
    }
}
