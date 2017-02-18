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
            int pid = -1;
            using (var proc = Process.GetCurrentProcess())
                pid = proc.Id;
            return pid;
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
