using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

public static class WindowActivityText
{
    public const int MaxAppNameChars = 48;

    public static int TitleCap(bool limitOn, int configured)
        => limitOn
            ? Math.Clamp(configured, 0, Constants.OscMaxMessageLength)
            : Constants.OscMaxMessageLength;

    public static string Compose(string? appName, string? windowTitle)
    {
        string name = SegmentWriter.Truncate(SegmentWriter.Tidy(appName), MaxAppNameChars);
        string title = SegmentWriter.Tidy(windowTitle);

        return new SegmentWriter()
            .Field(
                OscText.Value($"'{name}'"),
                OscText.Value(title.Length == 0 ? null : $"({title})"))
            .Text;
    }
}

public class WindowActivityModule : vrcosc_magicchatbox.Services.IWindowActivityService
{
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint SHGFI_DISPLAYNAME = 0x00000200;
    private static readonly uint SHFileInfoSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<SHFILEINFO>();

    private const int MaxProcessDisplayCacheEntries = 64;

    private bool _usedNewMethod = false;
    private readonly string _vrChatDirectory;
    private readonly string _vrChatExecutable = "vrchat.exe";

    private readonly object _foregroundStateLock = new();
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;
    private uint _lastForegroundPid;
    private string? _lastForegroundProcessName;

    private readonly ConcurrentDictionary<(int Pid, string ShortName), ProcessDisplayCacheEntry> _processDisplayCache = new();
    private readonly ConcurrentDictionary<string, ProcessInfo> _scannedAppsLookup = new(StringComparer.Ordinal);
    private INotifyCollectionChanged? _trackedScannedApps;

    private readonly ISettingsProvider<WindowActivitySettings> _settingsProvider;
    public WindowActivitySettings Settings => _settingsProvider.Value;
    public void SaveSettings() => _settingsProvider.Save();

    private readonly WindowActivityDisplayState WA;
    private readonly IAppState AppState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService? _toast;
    private DateTime _waLastErrorToast = DateTime.MinValue;

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
        if (string.Equals(Settings.GlobalRegex, WindowActivitySettings.LegacyDefaultGlobalRegex, StringComparison.Ordinal))
            Settings.GlobalRegex = WindowActivitySettings.DefaultGlobalRegex;

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

        AttachScannedAppsTracking();
    }

    private void AttachScannedAppsTracking()
    {
        HookScannedAppsCollection();
        RebuildScannedAppsLookup();
        WA.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WindowActivityDisplayState.ScannedApps))
            {
                HookScannedAppsCollection();
                RebuildScannedAppsLookup();
            }
        };
    }

    private void HookScannedAppsCollection()
    {
        if (_trackedScannedApps != null)
            _trackedScannedApps.CollectionChanged -= ScannedApps_CollectionChanged;

        _trackedScannedApps = WA.ScannedApps;

        if (_trackedScannedApps != null)
            _trackedScannedApps.CollectionChanged += ScannedApps_CollectionChanged;
    }

    private void RebuildScannedAppsLookup()
    {
        _scannedAppsLookup.Clear();
        var apps = WA.ScannedApps;
        if (apps == null)
            return;

        foreach (ProcessInfo app in apps)
            _scannedAppsLookup[app.ProcessName] = app;
    }

    private void ScannedApps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (ProcessInfo item in e.NewItems)
                        _scannedAppsLookup[item.ProcessName] = item;
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (ProcessInfo item in e.OldItems)
                        _scannedAppsLookup.TryRemove(item.ProcessName, out _);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (ProcessInfo item in e.OldItems)
                        _scannedAppsLookup.TryRemove(item.ProcessName, out _);
                if (e.NewItems != null)
                    foreach (ProcessInfo item in e.NewItems)
                        _scannedAppsLookup[item.ProcessName] = item;
                break;
            default:
                RebuildScannedAppsLookup();
                break;
        }
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

    private string ConstructReturnString(ProcessInfo? existingProcessInfo, string processName, string windowTitle)
    {
        try
        {
            if (existingProcessInfo == null)
            {
                AddNewProcessToViewModel(processName, windowTitle);
                return WindowActivityText.Compose(processName, null);
            }
            else
            {
                if (WA.LastProcessFocused == null || WA.LastProcessFocused.ProcessName != processName)
                {
                    existingProcessInfo.FocusCount++;
                    WA.LastProcessFocused = existingProcessInfo;
                }

                if (existingProcessInfo.IsPrivateApp)
                {
                    if (Settings.HideOutputWhenPrivateApp)
                    {
                        return string.Empty;
                    }

                    return AppState.IsVRRunning
                        ? Settings.PrivateNameVR
                        : Settings.PrivateName;
                }

                windowTitle = ApplyCustomRegex(existingProcessInfo, windowTitle);

                bool titleCheck = CheckTitleCondition(existingProcessInfo, windowTitle);

                string title = titleCheck && Settings.TitleScan ? windowTitle : string.Empty;

                return existingProcessInfo.ApplyCustomAppName && !string.IsNullOrEmpty(existingProcessInfo.CustomAppName)
                    ? WindowActivityText.Compose(existingProcessInfo.CustomAppName, title)
                    : WindowActivityText.Compose(processName, title);
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

    private string ApplyGlobalRegex(string windowTitle)
    {
        if (!Settings.UseGlobalRegex
            || string.IsNullOrWhiteSpace(Settings.GlobalRegex)
            || string.IsNullOrEmpty(windowTitle))
            return windowTitle;

        return TitleContentFilter.ApplyRegexTransform(windowTitle, Settings.GlobalRegex);
    }

    private static string ApplyCustomRegex(ProcessInfo process, string windowTitle)
    {
        if (!process.UseCustomRegex
            || string.IsNullOrWhiteSpace(process.CustomRegex)
            || string.IsNullOrEmpty(windowTitle))
            return windowTitle;

        return TitleContentFilter.ApplyRegexTransform(windowTitle, process.CustomRegex);
    }

    private string FormatWindowTitle(string fullTitle, ProcessInfo? process = null)
    {
        fullTitle = fullTitle?.Trim() ?? string.Empty;
        fullTitle = ApplyGlobalRegex(fullTitle);

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

        return SegmentWriter.Truncate(
            fullTitle,
            WindowActivityText.TitleCap(Settings.LimitTitleOnApp, Settings.MaxShowTitleCount));
    }

    private static string ApplyPerAppFilter(string text, ProcessInfo process)
    {
        return process.ContentFilterMode switch
        {
            1 => TitleContentFilter.MatchesAny(text, process.ContentFilter) ? string.Empty : text,
            2 => TitleContentFilter.MatchesAny(text, process.ContentFilter) ? text : string.Empty,
            3 => TitleContentFilter.RemoveMatches(text, process.ContentFilter),
            _ => text
        };
    }

    private string ApplyTitleFilters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string originalText = text;
        string output = text;
        bool hasIncludeRules = false;
        bool matchedInclude = false;

        foreach (var rule in Settings.TitleFilters)
        {
            if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Pattern))
                continue;

            bool matches = TitleContentFilter.MatchesAny(originalText, rule.Pattern);

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

        foreach (var rule in Settings.TitleFilters)
        {
            if (!rule.IsEnabled || string.IsNullOrWhiteSpace(rule.Pattern) || rule.Mode != FilterMode.Remove)
                continue;

            output = TitleContentFilter.RemoveMatches(output, rule.Pattern);
        }

        return output;
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
            var task = Task.Run(() =>
            {
                AutomationElement element = AutomationElement.FromHandle(hwnd);
                return element?.Current.Name ?? "Unknown";
            });

            if (task.Wait(TimeSpan.FromSeconds(2)))
                return task.Result;

            return "Unknown";        }
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
                SHFileInfoSize,
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
                processName = ResolveCachedProcessDisplayName(process);
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

    private string ResolveCachedProcessDisplayName(Process process)
    {
        var cacheKey = (process.Id, process.ProcessName);

        if (_processDisplayCache.TryGetValue(cacheKey, out ProcessDisplayCacheEntry cached))
        {
            _usedNewMethod = cached.UsedNewMethod;
            return cached.ProcessName;
        }

        string processName = TryGetFileDescriptionOrProcessName(process);
        processName = GetNameFromSHGetFileInfo(process, processName);

        if (_processDisplayCache.Count >= MaxProcessDisplayCacheEntries)
            _processDisplayCache.Clear();

        _processDisplayCache[cacheKey] = new ProcessDisplayCacheEntry(processName, _usedNewMethod);

        return processName;
    }

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
            Process? process = null;
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

                string processName;
                string? cachedProcessName = null;

                lock (_foregroundStateLock)
                {
                    if (_lastForegroundHwnd == hwnd && _lastForegroundPid == pid)
                        cachedProcessName = _lastForegroundProcessName;
                }

                if (cachedProcessName != null)
                {
                    processName = cachedProcessName;
                }
                else
                {
                    try { process = Process.GetProcessById((int)pid); }
                    catch (ArgumentException) { process = null; }

                    if (process == null)
                    {
                        errorInProcess = true;
                        continue;
                    }

                    using (process)
                    {
                        processName = GetProcessName(hwnd, process, attempt);
                    }

                    process = null;

                    lock (_foregroundStateLock)
                    {
                        _lastForegroundHwnd = hwnd;
                        _lastForegroundPid = pid;
                        _lastForegroundProcessName = processName;
                    }
                }

                string windowTitle = "";

                ProcessInfo? existingProcessInfo = _scannedAppsLookup.TryGetValue(processName, out ProcessInfo? found)
                    ? found
                    : null;

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

    private readonly record struct ProcessDisplayCacheEntry(string ProcessName, bool UsedNewMethod);

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
