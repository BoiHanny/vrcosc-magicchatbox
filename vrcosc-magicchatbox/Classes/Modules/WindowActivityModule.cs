using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

public static class WindowActivityModule
{
    // Constants and Structs
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint SHGFI_DISPLAYNAME = 0x00000200;

    private static bool _usedNewMethod = false;
    private static readonly string _vrChatDirectory = @"C:\Steam\steamapps\common\VRChat";
    private static readonly string _vrChatExecutable = "vrchat.exe";

    private static void AddNewProcessToViewModel(string processName, string windowTitle)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessInfo processInfo = new ProcessInfo
                {
                    LastTitle = windowTitle,
                    ShowTitle = ViewModel.Instance.AutoShowTitleOnNewApp,
                    ProcessName = processName,
                    UsedNewMethod = _usedNewMethod,
                    ApplyCustomAppName = false,
                    CustomAppName = "",
                    IsPrivateApp = false,
                    FocusCount = 1
                };

                ViewModel.Instance.ScannedApps.Add(processInfo);
                ViewModel.Instance.LastProcessFocused = processInfo;
            });
        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in AddNewProcessToViewModel: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
        }
    }

    private static bool CheckTitleCondition(ProcessInfo existingProcessInfo, string windowTitle)
    {
        bool showTitle1stCheck = ViewModel.Instance.WindowActivityTitleScan
                                && existingProcessInfo.ShowTitle
                                && !string.IsNullOrEmpty(windowTitle);
        if (!ViewModel.Instance.TitleOnAppVR && ViewModel.Instance.IsVRRunning)
        {
            showTitle1stCheck = false;
        }

        return showTitle1stCheck;
    }

    private static string ConstructReturnString(ProcessInfo existingProcessInfo, string processName, string windowTitle)
    {
        try
        {
            if (existingProcessInfo == null)
            {
                AddNewProcessToViewModel(processName, windowTitle);
                return $"'{processName}'";
            }
            else
            {
                if (ViewModel.Instance.LastProcessFocused.ProcessName != processName)
                {
                    existingProcessInfo.FocusCount++;
                    ViewModel.Instance.LastProcessFocused = existingProcessInfo;
                }

                bool titleCheck = CheckTitleCondition(existingProcessInfo, windowTitle);

                if (existingProcessInfo.IsPrivateApp)
                {
                    if (ViewModel.Instance.IsVRRunning)
                    {
                        return ViewModel.Instance.WindowActivityPrivateNameVR;
                    }
                    else
                    {
                        return ViewModel.Instance.WindowActivityPrivateName;
                    }

                }
                else if (existingProcessInfo.ApplyCustomAppName && !string.IsNullOrEmpty(existingProcessInfo.CustomAppName))
                {
                    return "'" + existingProcessInfo.CustomAppName + "'" + (titleCheck && ViewModel.Instance.WindowActivityTitleScan ? " (" + windowTitle + ")" : string.Empty);
                }
                else
                {
                    return "'" + processName + "'" + (titleCheck && ViewModel.Instance.WindowActivityTitleScan ? " ( " + windowTitle + ")" : string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in ConstructReturnString: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return processName;
        }

    }

    private static string FormatWindowTitle(string fullTitle)
    {
        // Truncate the application name after the hyphen.
        int index = fullTitle.LastIndexOf(" - ");
        if (index > 0)
        {
            fullTitle = fullTitle.Substring(0, index);
        }

        // Check length for further truncation.
        if (ViewModel.Instance.LimitTitleOnApp && fullTitle.Length > ViewModel.Instance.MaxShowTitleCount)
        {
            fullTitle = fullTitle.Substring(0, ViewModel.Instance.MaxShowTitleCount) + "...";
        }

        return fullTitle;
    }

    private static string GetFileDescription(string filePath)
    {
        try
        {
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
            return string.IsNullOrEmpty(fileVersionInfo.FileDescription)
                ? Path.GetFileName(filePath)
                : fileVersionInfo.FileDescription;
        }
        catch (Exception)
        {
            return Path.GetFileName(filePath);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static string GetNameFromAutomationElement(IntPtr hwnd)
    {

        try
        {
            AutomationElement element = AutomationElement.FromHandle(hwnd);
            if (element != null)
            {
                return element.Current.Name;
            }
            return "Unknown";
        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetNameFromAutomationElement (HandleID:{{hwnd}}): {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return "Unknown";
        }



    }

    private static string GetNameFromSHGetFileInfo(Process process, string processName)
    {
        if (string.IsNullOrEmpty(processName) || processName == "Unknown")
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(
                process.MainModule.FileName,
                FILE_ATTRIBUTE_NORMAL,
                ref shinfo,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(shinfo),
                SHGFI_DISPLAYNAME);

            if (result != IntPtr.Zero)
            {
                return shinfo.szDisplayName;
            }
        }
        return processName;
    }

    private static string GetProcessName(IntPtr hwnd, Process process, int attempts)
    {


        string processName = "Unknown";
        try
        {
            if (process.ProcessName == "ApplicationFrameHost" && ViewModel.Instance.ApplicationHookV2)
            {
                processName = GetNameFromAutomationElement(hwnd);
                _usedNewMethod = true;
            }
            else
            {
                processName = TryGetFileDescriptionOrProcessName(process);
                processName = GetNameFromSHGetFileInfo(process, processName);
            }

            processName = RemoveExeExtension(processName);
            return processName;
        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetProcessName ({processName}): {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return processName;
        }

    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // P/Invoke Methods
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Private Methods
    private static string GetWindowTitle(IntPtr hwnd)
    {
        try
        {
            int length = GetWindowTextLength(hwnd) + 1;
            StringBuilder sb = new StringBuilder(length);

            if (GetWindowText(hwnd, sb, length) > 0)
            {
                return FormatWindowTitle(sb.ToString());
            }

            return "";
        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetWindowTitle: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return "";
        }

    }

    private static string RemoveExeExtension(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName.Substring(0, processName.Length - 4)
            : processName;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    private static string TryGetFileDescriptionOrProcessName(Process process)
    {
        if (ViewModel.Instance.ApplicationHookV2)
        {
            try
            {
                _usedNewMethod = true;
                return GetFileDescription(process.MainModule.FileName);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _usedNewMethod = false;
                return process.ProcessName;
            }
        }
        _usedNewMethod = false;
        return process.ProcessName;
    }

    // Public Methods
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



    public static string GetForegroundProcessName()
    {
        try
        {
            const int maxRetries = 3;
            IntPtr hwnd = IntPtr.Zero;
            Process process = null;
            bool errorInhwnd = false;
            bool errorInProcess = false;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                hwnd = GetForegroundWindow();

                if (hwnd == IntPtr.Zero)
                {
                    errorInhwnd = true;
                    continue;
                }

                GetWindowThreadProcessId(hwnd, out uint pid);
                process = Process.GetProcesses().FirstOrDefault(p => p.Id == pid);

                if (process == null)
                {
                    errorInProcess = true;
                    continue;
                }

                string processName = GetProcessName(hwnd, process, attempt);
                string windowTitle = "";

                ProcessInfo existingProcessInfo = null;
                existingProcessInfo = ViewModel.Instance.ScannedApps?.FirstOrDefault(info => info.ProcessName == processName);

                if (existingProcessInfo == null)
                {
                    windowTitle = GetWindowTitle(hwnd);
                }
                else
                {
                    if (existingProcessInfo.ShowTitle)
                    {
                        windowTitle = GetWindowTitle(hwnd);
                    }
                }
                ViewModel.Instance.ErrorInWindowActivity = false;
                return ConstructReturnString(existingProcessInfo, processName, windowTitle);

            }

            ViewModel.Instance.ErrorInWindowActivity = true;

            StringBuilder errorMsgBuilder = new StringBuilder($"Couldn't retrieve app title after 3 attempts || HandleID: {hwnd}");

            if (process != null)
            {
                errorMsgBuilder.Append($" {process}");
            }

            if (errorInhwnd)
            {
                errorMsgBuilder.Append(", Error in fetching the focused app");
            }

            if (errorInProcess)
            {
                errorMsgBuilder.Append(", Error in collecting process data");
            }

            string errormsg = errorMsgBuilder.ToString();


            Logging.WriteException(new Exception(errormsg), MSGBox: false);
            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return "'An app'";

        }
        catch (Exception ex)
        {
            ViewModel.Instance.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetForegroundProcessName: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);

            if (ViewModel.Instance.ErrorInWindowActivity)
            {
                errormsg += ", error in window activity.";
            }

            ViewModel.Instance.ErrorInWindowActivityMsg = errormsg;
            return "'An app'";
        }
    }

    public static bool IsOSCServerSuspended()
    {
        var process = Process.GetProcessesByName("install")
                             .FirstOrDefault(p => p.MainModule.FileName.EndsWith("install.exe", StringComparison.OrdinalIgnoreCase));

        if (process != null)
        {
            var processPath = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath) && Path.GetDirectoryName(processPath) == _vrChatDirectory)
            {
                if (File.Exists(Path.Combine(_vrChatDirectory, _vrChatExecutable)))
                {
                    return process.Responding == false;
                }
            }
        }

        return false;
    }

    public static void KillOSCServer()
    {
        var process = Process.GetProcessesByName("install")
                             .FirstOrDefault(p => p.MainModule.FileName.EndsWith("install.exe", StringComparison.OrdinalIgnoreCase));

        if (process != null)
        {
            var processPath = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath) && Path.GetDirectoryName(processPath) == _vrChatDirectory)
            {
                if (File.Exists(Path.Combine(_vrChatDirectory, _vrChatExecutable)))
                {
                    process.Kill();
                }
            }
        }
    }


    public static int ResetWindowActivity()
    {
        int removed = 0;
        var result = MessageBox.Show(
            "Are you sure you want to delete all the history and settings of the Window Activity integration?",
            "Confirmation",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Exclamation);

        if (result == MessageBoxResult.OK)
        {
            ViewModel.Instance.ScannedApps.Clear();
            removed++;
        }
        return removed;
    }



    public static int SmartCleanup()
    {
        int removed = 0;
        for (int i = ViewModel.Instance.ScannedApps.Count - 1; i >= 0; i--)
        {
            var app = ViewModel.Instance.ScannedApps[i];
            if (app.FocusCount < 15 &&
                !app.IsPrivateApp &&
                !app.ApplyCustomAppName &&
                string.IsNullOrEmpty(app.CustomAppName))
            {
                ViewModel.Instance.ScannedApps.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

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
    }




}
