using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public static class WindowActivity
    {

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);



        public static string GetForegroundProcessName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();

                if (hwnd == null)
                {
                    return "Unknown";
                    Logging.WriteInfo("Unknown GetForegroundProcessName", makeVMDump: false, MSGBox: false);
                }


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

        public static bool IsVRRunning()
        {
            try
            {
                Process[] pname = Process.GetProcessesByName("vrmonitor");
                if (pname.Length == 0)
                    return false;
                else
                    return true;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }


    }
}
