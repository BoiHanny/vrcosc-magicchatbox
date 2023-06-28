using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
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

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        private const uint SHGFI_DISPLAYNAME = 0x00000200;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        public static string GetForegroundProcessName()
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    IntPtr hwnd = GetForegroundWindow();

                    if (hwnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    GetWindowThreadProcessId(hwnd, out uint pid);

                    Process process = Process.GetProcesses().FirstOrDefault(p => p.Id == pid);
                    string processName = "Unknown";
                    if (process != null)
                    {

                        bool usedNewMethod = false;
                        if (process.ProcessName == "ApplicationFrameHost" && ViewModel.Instance.ApplicationHookV2 && attempt == 0)
                        {
                            // If the process is an Application Frame Host, use UI Automation to get the window title
                            AutomationElement element = AutomationElement.FromHandle(hwnd);
                            if (element != null)
                            {
                                processName = element.Current.Name;
                                usedNewMethod = true;
                            }
                        }
                        else
                        {
                            if (ViewModel.Instance.ApplicationHookV2)
                            {
                                try
                                {
                                    processName = GetFileDescription(process.MainModule.FileName);
                                    usedNewMethod = true;
                                }
                                catch (System.ComponentModel.Win32Exception ex)
                                {
                                    processName = process.ProcessName;
                                }
                            }
                            else
                            {
                                processName = process.ProcessName;
                            }

                            if (string.IsNullOrEmpty(processName) || processName == "Unknown")
                            {
                                // If processName is null, empty, or "Unknown", try to get it using SHGetFileInfo
                                SHFILEINFO shinfo = new SHFILEINFO();
                                IntPtr result = SHGetFileInfo(process.MainModule.FileName, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)System.Runtime.InteropServices.Marshal.SizeOf(shinfo), SHGFI_DISPLAYNAME);
                                if (result != IntPtr.Zero)
                                {
                                    processName = shinfo.szDisplayName;
                                }
                            }
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

                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);

                }
            }

            Logging.WriteException(new Exception("Couldn't get application title after 3 tries"), makeVMDump: false, MSGBox: false);
            return "Unknown";
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

        public static int SmartCleanup()
        {
            int removed = 0;
            for (int i = ViewModel.Instance.ScannedApps.Count - 1; i >= 0; i--)
            {
                var app = ViewModel.Instance.ScannedApps[i];
                if (app.FocusCount < 15 && !app.IsPrivateApp && !app.ApplyCustomAppName && string.IsNullOrEmpty(app.CustomAppName))
                {
                    ViewModel.Instance.ScannedApps.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public static int CleanAndKeepAppsWithSettings()
        {
            int removed = 0;
            for (int i = ViewModel.Instance.ScannedApps.Count - 1; i >= 0; i--)
            {
                var app = ViewModel.Instance.ScannedApps[i];
                if (!app.IsPrivateApp && !app.ApplyCustomAppName && string.IsNullOrEmpty(app.CustomAppName))
                {
                    ViewModel.Instance.ScannedApps.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public static int ResetWindowActivity()
        {
            int removed = 0;
            var result = MessageBox.Show("Are you sure you want to delete all the history and settings of the Window Activity integration?", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

            if (result == MessageBoxResult.OK)
            {
                ViewModel.Instance.ScannedApps.Clear();
                removed++;
            }
            return removed;
        }

    }
}
