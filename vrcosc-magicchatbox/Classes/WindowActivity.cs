using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                    return "Unknown";
                }

                GetWindowThreadProcessId(hwnd, out uint pid);

                string foregroundProcessName = string.Empty;
                Process process = Process.GetProcesses().FirstOrDefault(p => p.Id == pid);

                if (process != null)
                {
                    string processName;
                    bool usedNewMethod = false;

                    if (ViewModel.Instance.ApplicationHookV2)
                    {
                        try
                        {
                            processName = GetFileDescription(process.MainModule.FileName);
                            usedNewMethod = true;
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            if (ex.Message != "Access is denied.")
                            {
                                throw;
                            }

                            processName = process.ProcessName;
                        }
                    }
                    else
                    {
                        processName = process.ProcessName;
                    }

                    processName = RemoveExeExtension(processName);

                    // Rest of the method remains unchanged
                    ProcessInfo existingProcessInfo = ViewModel.Instance.ScannedApps.FirstOrDefault(info => info.ProcessName == processName);

                    if (existingProcessInfo == null)
                    {
                        ProcessInfo processInfo = new ProcessInfo
                        {
                            ProcessName = processName,
                            UsedNewMethod = usedNewMethod,
                            ApplyCustomAppName = false,
                            CustomAppName = string.Empty,
                            IsPrivateApp = false,
                            FocusCount = 1
                        };

                        ViewModel.Instance.ScannedApps.Add(processInfo);
                        ViewModel.Instance.LastProcessFocused = processInfo;
                        return processName;
                    }
                    else
                    {
                        if (ViewModel.Instance.LastProcessFocused.ProcessName != processName)
                        {
                            existingProcessInfo.FocusCount++;
                            ViewModel.Instance.LastProcessFocused = existingProcessInfo;
                        }

                        if (existingProcessInfo.IsPrivateApp)
                        {
                            return "Private App";
                        }
                        else if (existingProcessInfo.ApplyCustomAppName)
                        {
                            return existingProcessInfo.CustomAppName;
                        }
                        else
                        {
                            return processName;
                        }
                    }
                }

                return "Unknown";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }


        public static void SortScannedAppsByFocusCount()
        {
            ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>(
                ViewModel.Instance.ScannedApps.OrderByDescending(p => p.FocusCount));
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

        private static string RemoveExeExtension(string processName)
        {
            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4)
                : processName;
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
