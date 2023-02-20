using System;
using System.Diagnostics;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public class WindowActivity
    {
        private ViewModel _VM;
        public WindowActivity(ViewModel vm)
        {
            _VM = vm;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public string GetForegroundProcessName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();

                if (hwnd == null)
                    return "Unknown";

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);

                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
                {
                    if (p.Id == pid)
                        return p.ProcessName;
                }

                return "Unknown";
            }
            catch (Exception)
            {

                return "Unknown Error";
            }
        }

        public bool IsVRRunning()
        {
            try
            {
                Process[] pname = Process.GetProcessesByName("vrmonitor");
                if (pname.Length == 0)
                    return false;
                else
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


    }
}
