using System;
using System.Diagnostics;
using System.IO;
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

                if (hwnd == IntPtr.Zero)
                {
                    Logging.WriteInfo("Unknown GetForegroundProcessName", makeVMDump: false, MSGBox: false);
                    return "Unknown";
                }

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);

                string processName = "Unknown";

                if (ViewModel.Instance.GetForegroundProcessNew)
                {
                    try
                    {
                        foreach (Process p in Process.GetProcesses())
                        {
                            if (p.Id == pid)
                            {
                                processName = GetFileDescription(p.MainModule.FileName);
                                break;
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        if (ex.Message != "Access is denied.")
                        {
                            throw;
                        }
                    }

                    // If the new method doesn't give a valid result, fall back to the old method.
                    if (processName == "Unknown")
                    {
                        processName = GetProcessNameOldMethod(pid);
                    }
                }
                else
                {
                    processName = GetProcessNameOldMethod(pid);
                }

                return processName;
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        private static string GetProcessNameOldMethod(uint pid)
        {
            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
            {
                if (p.Id == pid)
                {
                    return p.ProcessName;
                }
            }

            return "Unknown";
        }
        private static string GetFileDescription(string filePath)
        {
            try
            {
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return string.IsNullOrEmpty(fileVersionInfo.FileDescription) ? Path.GetFileName(filePath) : fileVersionInfo.FileDescription;
            }
            catch (Exception)
            {
                return Path.GetFileName(filePath);
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
