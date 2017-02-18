using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Windows.Forms;

namespace LagfreeAgent
{
    public partial class DummyForm : Form
    {
        public DummyForm()
        {
            InitializeComponent();
            TheOnlyInstance = this;
        }

        IpcServerChannel chan;
        internal static DummyForm TheOnlyInstance;

        private void DummyForm_Load(object sender, EventArgs e)
        {
            try
            {
                chan = new IpcServerChannel(new Hashtable() { { "name", "LagfreeAgent" }, { "portName", "LagfreeAgent" }, { "authorizedGroup", "Everyone" } }, null, null);
                ChannelServices.RegisterChannel(chan, true);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(VisiblePids), "VisiblePids", WellKnownObjectMode.Singleton);
            }
            catch { Application.Exit(); }
        }

        private void DummyForm_Shown(object sender, EventArgs e)
        {
            Hide();
        }

        private void DummyForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ChannelServices.UnregisterChannel(chan);
            chan = null;
        }
    }
}
