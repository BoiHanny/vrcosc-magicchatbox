using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Service that tracks focused Windows processes, resolves display names, and produces formatted
/// window-activity strings for the VRChat chat overlay.
/// </summary>
public class WindowActivityModule : vrcosc_magicchatbox.Services.IWindowActivityService
{
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint SHGFI_DISPLAYNAME = 0x00000200;
    private static readonly object InvalidCustomRegexLock = new();
    private static readonly HashSet<string> InvalidCustomRegexLogged = new(StringComparer.Ordinal);

    private bool _usedNewMethod = false;
    private readonly string _vrChatDirectory;
    private readonly string _vrChatExecutable = "vrchat.exe";

    private readonly ISettingsProvider<WindowActivitySettings> _settingsProvider;
    public WindowActivitySettings Settings => _settingsProvider.Value;
    public void SaveSettings() => _settingsProvider.Save();

    private readonly WindowActivityDisplayState WA;
    private readonly IAppState AppState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService? _toast;
    private DateTime _waLastErrorToast = DateTime.MinValue;
    private string? _lastInvalidGlobalRegex;

    public WindowActivityModule(
        ISettingsProvider<WindowActivitySettings> settingsProvider,
        WindowActivityDisplayState windowActivityDisplay,
        IAppState appState,
        IEnvironmentService environmentService,
        IUiDispatcher dispatcher,
        IPrivacyConsentService consentService,
        IToastService? toast = null)
    {
        _settingsProvider = settingsProvider;
        WA = windowActivityDisplay;
        AppState = appState;
        _vrChatDirectory = environmentService.VrcPath;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _toast = toast;

        _consentService.ConsentChanged += (_, e) =>
        {
            if (e.Hook == PrivacyHook.WindowActivity && e.NewState == ConsentState.Denied)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    WA.ScannedApps.Clear();
                    WA.LastProcessFocused = null;
                });
            }
        };
    }

    private void AddNewProcessToViewModel(string processName, string windowTitle)
    {
        try
        {
            _dispatcher.BeginInvoke(() =>
            {
                ProcessInfo processInfo = new ProcessInfo
                {
                    LastTitle = windowTitle,
                    ShowTitle = Settings.AutoShowTitleOnNewApp,
                    ProcessName = processName,
                    UsedNewMethod = _usedNewMethod,
                    ApplyCustomAppName = false,
                    CustomAppName = "",
                    IsPrivateApp = false,
                    FocusCount = 1
                };

                WA.ScannedApps.Add(processInfo);
                WA.LastProcessFocused = processInfo;
            });
        }
        catch (Exception ex)
        {
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in AddNewProcessToViewModel: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
        }
    }

    private bool CheckTitleCondition(ProcessInfo existingProcessInfo, string windowTitle)
    {
        bool showTitle1stCheck = Settings.TitleScan
                                && existingProcessInfo.ShowTitle
                                && !string.IsNullOrEmpty(windowTitle);
        if (!Settings.TitleOnAppVR && AppState.IsVRRunning)
        {
            showTitle1stCheck = false;
        }

        return showTitle1stCheck;
    }

    private string ConstructReturnString(ProcessInfo existingProcessInfo, string processName, string windowTitle)
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
                if (WA.LastProcessFocused == null || WA.LastProcessFocused.ProcessName != processName)
                {
                    existingProcessInfo.FocusCount++;
                    WA.LastProcessFocused = existingProcessInfo;
                }

                // Global regex is applied before title length limiting in FormatWindowTitle.
                windowTitle = ApplyCustomRegex(existingProcessInfo, windowTitle);

                bool titleCheck = CheckTitleCondition(existingProcessInfo, windowTitle);

                if (existingProcessInfo.IsPrivateApp)
                {
                    if (AppState.IsVRRunning)
                    {
                        return Settings.PrivateNameVR;
                    }
                    else
                    {
                        return Settings.PrivateName;
                    }

                }
                else if (existingProcessInfo.ApplyCustomAppName && !string.IsNullOrEmpty(existingProcessInfo.CustomAppName))
                {
                    return "'" + existingProcessInfo.CustomAppName + "'" + (titleCheck && Settings.TitleScan ? " (" + windowTitle + ")" : string.Empty);
                }
                else
                {
                    return "'" + processName + "'" + (titleCheck && Settings.TitleScan ? " ( " + windowTitle + ")" : string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in ConstructReturnString: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return processName;
        }

    }

    /// <summary>
    /// Applies the global regex to a window title. Runs BEFORE per-app regex.
    /// If the regex matches, returns the first capture group; otherwise the original title.
    /// </summary>
    private string ApplyGlobalRegex(string windowTitle)
    {
        if (!Settings.UseGlobalRegex
            || string.IsNullOrWhiteSpace(Settings.GlobalRegex)
            || string.IsNullOrEmpty(windowTitle))
            return windowTitle;

        try
        {
            var match = GetGlobalRegex().Match(windowTitle);
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                return match.Groups[1].Value;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            if (!string.Equals(_lastInvalidGlobalRegex, Settings.GlobalRegex, StringComparison.Ordinal))
            {
                _lastInvalidGlobalRegex = Settings.GlobalRegex;
                Logging.WriteException(ex, MSGBox: false);
            }
        }
        return windowTitle;
    }

    /// <summary>
    /// Applies a per-app custom regex to the window title. If the regex matches,
    /// returns the first capture group; otherwise returns the original title unchanged.
    /// </summary>
    private static string ApplyCustomRegex(ProcessInfo process, string windowTitle)
    {
        if (!process.UseCustomRegex
            || string.IsNullOrWhiteSpace(process.CustomRegex)
            || string.IsNullOrEmpty(windowTitle))
            return windowTitle;

        try
        {
            var match = Regex.Match(windowTitle, process.CustomRegex, RegexOptions.None, TimeSpan.FromMilliseconds(50));
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                return match.Groups[1].Value;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            lock (InvalidCustomRegexLock)
            {
                if (!InvalidCustomRegexLogged.Add(process.CustomRegex))
                    return windowTitle;
            }

            Logging.WriteException(ex, MSGBox: false);
        }
        return windowTitle;
    }

    private string FormatWindowTitle(string fullTitle, ProcessInfo? process = null)
    {
        fullTitle = fullTitle?.Trim() ?? string.Empty;
        fullTitle = ApplyGlobalRegex(fullTitle);

        // Apply per-app content filter first (takes priority)
        if (process != null && process.HasContentFilter && !string.IsNullOrWhiteSpace(fullTitle))
        {
            fullTitle = ApplyPerAppFilter(fullTitle, process);
            if (string.IsNullOrEmpty(fullTitle))
                return string.Empty;
        }

        if (Settings.EnableTitleFilters && Settings.TitleFilters.Count > 0 && !string.IsNullOrWhiteSpace(fullTitle))
        {
            fullTitle = ApplyTitleFilters(fullTitle);
            if (string.IsNullOrEmpty(fullTitle))
                return string.Empty;
        }

        if (Settings.LimitTitleOnApp && fullTitle.Length > Settings.MaxShowTitleCount)
        {
            fullTitle = fullTitle.Substring(0, Settings.MaxShowTitleCount) + "...";
        }

        return fullTitle;
    }

    private static string ApplyPerAppFilter(string text, ProcessInfo process)
    {
        var keywords = process.ContentFilter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (keywords.Length == 0)
            return text;

        bool anyMatch = keywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));

        return process.ContentFilterMode switch
        {
            1 => anyMatch ? string.Empty : text,  // Exclude: hide when matches
            2 => anyMatch ? text : string.Empty,  // Include: show only when matches
            _ => text
        };
    }

    private string ApplyTitleFilters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        bool hasIncludeRules = false;
        bool matchedInclude = false;

        foreach (var rule in Settings.TitleFilters)
        {
            if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Pattern))
                continue;

            bool matches = text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);

            if (rule.Mode == FilterMode.Exclude && matches)
                return string.Empty;

            if (rule.Mode == FilterMode.Include)
            {
                hasIncludeRules = true;
                if (matches)
                    matchedInclude = true;
            }
        }

        if (hasIncludeRules && !matchedInclude)
            return string.Empty;

        return text;
    }

    private string GetFileDescription(string filePath)
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

    private string GetNameFromAutomationElement(IntPtr hwnd)
    {
        try
        {
            // UIAutomation can block for 20+ seconds if the target app is hung.
            // Run on a background thread with a 2-second timeout.
            var task = Task.Run(() =>
            {
                AutomationElement element = AutomationElement.FromHandle(hwnd);
                return element?.Current.Name ?? "Unknown";
            });

            if (task.Wait(TimeSpan.FromSeconds(2)))
                return task.Result;

            return "Unknown"; // Timed out — app is likely hung
        }
        catch (Exception ex)
        {
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetNameFromAutomationElement (HandleID:{hwnd}): {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return "Unknown";
        }
    }

    private string GetNameFromSHGetFileInfo(Process process, string processName)
    {
        if (string.IsNullOrEmpty(processName) || processName == "Unknown")
        {
            string? processPath = TryGetProcessPath(process);
            if (string.IsNullOrEmpty(processPath))
            {
                return processName;
            }

            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(
                processPath,
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

    private string GetProcessName(IntPtr hwnd, Process process, int attempts)
    {


        string processName = "Unknown";
        try
        {
            if (process.ProcessName == "ApplicationFrameHost" && Settings.ApplicationHookV2)
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
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetProcessName ({processName}): {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return processName;
        }

    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private string GetWindowTitle(IntPtr hwnd, ProcessInfo? process = null)
    {
        try
        {
            int length = GetWindowTextLength(hwnd) + 1;
            StringBuilder sb = new StringBuilder(length);

            if (GetWindowText(hwnd, sb, length) > 0)
            {
                return FormatWindowTitle(sb.ToString(), process);
            }

            return "";
        }
        catch (Exception ex)
        {
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetWindowTitle: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return "";
        }

    }

    private string RemoveExeExtension(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName.Substring(0, processName.Length - 4)
            : processName;
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    private string TryGetFileDescriptionOrProcessName(Process process)
    {
        if (Settings.ApplicationHookV2)
        {
            string? processPath = TryGetProcessPath(process);
            if (!string.IsNullOrEmpty(processPath))
            {
                _usedNewMethod = true;
                return GetFileDescription(processPath);
            }
        }
        _usedNewMethod = false;
        return process.ProcessName;
    }

    private Process? TryFindOscServerProcess(out string? processPath)
    {
        foreach (var process in Process.GetProcessesByName("install"))
        {
            string? candidatePath = TryGetProcessPath(process);
            if (string.IsNullOrEmpty(candidatePath))
            {
                continue;
            }

            if (candidatePath.EndsWith("install.exe", StringComparison.OrdinalIgnoreCase))
            {
                processPath = candidatePath;
                return process;
            }
        }

        processPath = null;
        return null;
    }

    public int CleanAndKeepAppsWithSettings()
    {
        int removed = 0;
        for (int i = WA.ScannedApps.Count - 1; i >= 0; i--)
        {
            var app = WA.ScannedApps[i];
            if (!app.IsPrivateApp && !app.ApplyCustomAppName && string.IsNullOrEmpty(app.CustomAppName))
            {
                WA.ScannedApps.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }



    public string GetForegroundProcessName()
    {
        if (!_consentService.IsApproved(PrivacyHook.WindowActivity))
            return "'An app'";

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
                try { process = Process.GetProcessById((int)pid); }
                catch (ArgumentException) { process = null; }

                if (process == null)
                {
                    errorInProcess = true;
                    continue;
                }

                string processName = GetProcessName(hwnd, process, attempt);
                string windowTitle = "";

                ProcessInfo existingProcessInfo = null;
                existingProcessInfo = WA.ScannedApps?.FirstOrDefault(info => info.ProcessName == processName);

                if (existingProcessInfo == null)
                {
                    windowTitle = GetWindowTitle(hwnd);
                }
                else
                {
                    if (existingProcessInfo.ShowTitle)
                    {
                        windowTitle = GetWindowTitle(hwnd, existingProcessInfo);
                    }
                }
                WA.ErrorInWindowActivity = false;
                return ConstructReturnString(existingProcessInfo, processName, windowTitle);

            }

            WA.ErrorInWindowActivity = true;

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
            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return "'An app'";

        }
        catch (Exception ex)
        {
            WA.ErrorInWindowActivity = true;
            string errormsg = $"Error in GetForegroundProcessName: {ex.Message}";
            Logging.WriteException(ex, MSGBox: false);

            if (WA.ErrorInWindowActivity)
            {
                errormsg += ", error in window activity.";
            }

            WA.ErrorInWindowActivityMsg = errormsg;
            FireWaErrorToast();
            return "'An app'";
        }
    }

    public bool IsOSCServerSuspended()
    {
        var process = TryFindOscServerProcess(out string? processPath);
        if (process != null)
        {
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

    public void KillOSCServer()
    {
        var process = TryFindOscServerProcess(out string? processPath);
        if (process != null)
        {
            if (!string.IsNullOrEmpty(processPath) && Path.GetDirectoryName(processPath) == _vrChatDirectory)
            {
                if (File.Exists(Path.Combine(_vrChatDirectory, _vrChatExecutable)))
                {
                    process.Kill();
                }
            }
        }
    }

    private void FireWaErrorToast()
    {
        if (_toast == null) return;
        if ((DateTime.UtcNow - _waLastErrorToast).TotalSeconds < 60) return;
        _waLastErrorToast = DateTime.UtcNow;
        _toast.Show("🪟 Window Activity", WA.ErrorInWindowActivityMsg, ToastType.Warning, key: "window-activity-error");
    }


    public int ResetWindowActivity()
    {
        int removed = 0;
        var result = MessageBox.Show(
            "Are you sure you want to delete all the history and settings of the Window Activity integration?",
            "Confirmation",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Exclamation);

        if (result == MessageBoxResult.OK)
        {
            WA.ScannedApps.Clear();
            removed++;
        }
        return removed;
    }



    public int SmartCleanup()
    {
        int removed = 0;
        for (int i = WA.ScannedApps.Count - 1; i >= 0; i--)
        {
            var app = WA.ScannedApps[i];
            if (app.FocusCount < 15 &&
                !app.IsPrivateApp &&
                !app.ApplyCustomAppName &&
                string.IsNullOrEmpty(app.CustomAppName))
            {
                WA.ScannedApps.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private Regex? _cachedGlobalRegex;
    private string? _cachedGlobalRegexPattern;

    private Regex GetGlobalRegex()
    {
        var pattern = Settings.GlobalRegex ?? string.Empty;
        if (_cachedGlobalRegex == null || !string.Equals(_cachedGlobalRegexPattern, pattern, StringComparison.Ordinal))
        {
            _cachedGlobalRegex = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50));
            _cachedGlobalRegexPattern = pattern;
        }
        return _cachedGlobalRegex;
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
