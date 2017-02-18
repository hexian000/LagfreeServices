using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace LagfreeAgent
{
    public class VisiblePids : MarshalByRefObject
    {
        public int GetPid()
        {
            return Process.GetCurrentProcess().Id;
        }

        public HashSet<int> Get()
        {
            return Win32Utils.GetVisiblePids();
        }

        public void Exit()
        {
            Application.Exit();
        }
    }
}
