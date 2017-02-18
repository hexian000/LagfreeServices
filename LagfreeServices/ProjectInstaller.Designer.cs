namespace LagfreeServices
{
    [System.ComponentModel.RunInstaller(true)]
    partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.spiHostProcessInst = new System.ServiceProcess.ServiceProcessInstaller();
            this.siHddServiceInst = new System.ServiceProcess.ServiceInstaller();
            this.siCpuServiceInst = new System.ServiceProcess.ServiceInstaller();
            this.siMemServiceInst = new System.ServiceProcess.ServiceInstaller();
            // 
            // spiHostProcessInst
            // 
            this.spiHostProcessInst.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.spiHostProcessInst.Password = null;
            this.spiHostProcessInst.Username = null;
            // 
            // siHddServiceInst
            // 
            this.siHddServiceInst.Description = "针对HDD动态调整进程IO优先级，提高IO响应速度";
            this.siHddServiceInst.DisplayName = "Lagfree HDD Manager Service";
            this.siHddServiceInst.ServiceName = "Lagfree HDD";
            this.siHddServiceInst.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // siCpuServiceInst
            // 
            this.siCpuServiceInst.Description = "动态调整后台进程CPU优先级类，提高CPU响应速度";
            this.siCpuServiceInst.DisplayName = "Lagfree CPU Manager Service";
            this.siCpuServiceInst.ServiceName = "Lagfree CPU";
            this.siCpuServiceInst.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // siMemServiceInst
            // 
            this.siMemServiceInst.Description = "当内存负载高时定时修剪工作集，提升缓存效率";
            this.siMemServiceInst.DisplayName = "Lagfree Memory Manager Service";
            this.siMemServiceInst.ServiceName = "Lagfree Memory";
            this.siMemServiceInst.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.spiHostProcessInst,
            this.siHddServiceInst,
            this.spiHostProcessInst,
            this.siCpuServiceInst,
            this.spiHostProcessInst,
            this.siMemServiceInst});

        }

        #endregion
        protected System.ServiceProcess.ServiceProcessInstaller spiHostProcessInst;
        protected System.ServiceProcess.ServiceInstaller siHddServiceInst;
        protected System.ServiceProcess.ServiceInstaller siCpuServiceInst;
        protected System.ServiceProcess.ServiceInstaller siMemServiceInst;

    }
}