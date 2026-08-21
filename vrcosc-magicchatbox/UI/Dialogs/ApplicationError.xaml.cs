using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    public partial class ApplicationError : Window
    {
        public AppUpdateState UpdateState { get; }
        private readonly IEnvironmentService _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUiDispatcher _dispatcher;
        private readonly IVersionService _versionService;
        private readonly INavigationService _nav;
        private readonly Exception _exception;
        private readonly DateTimeOffset _occurredAt;

        public ApplicationError(
            Exception ex,
            bool autoclose,
            int autoCloseinMiliSeconds,
            AppUpdateState updateState,
            IEnvironmentService env,
            IHttpClientFactory httpClientFactory,
            IUiDispatcher dispatcher,
            IVersionService versionService,
            INavigationService nav)
        {
            InitializeComponent();
            UpdateState = updateState;
            _env = env;
            _httpClientFactory = httpClientFactory;
            _dispatcher = dispatcher;
            _versionService = versionService;
            _nav = nav;
            DataContext = this;

            _exception = ex;
            _occurredAt = DateTimeOffset.Now;

            MainError.Text = string.IsNullOrWhiteSpace(ex.Message) ? "MagicChatbox hit an unexpected error." : ex.Message;
            ErrorType.Text = ex.GetType().FullName ?? string.Empty;
            CallStack.Text = string.IsNullOrWhiteSpace(ex.StackTrace) ? "(no stack trace was captured)" : ex.StackTrace;

            UpdateState.PropertyChanged += OnUpdateStateChanged;
            Closed += (_, _) => UpdateState.PropertyChanged -= OnUpdateStateChanged;
            RefreshRecoveryHint();

            if (autoclose)
                _ = AutoClose(autoCloseinMiliSeconds);

            _ = ManualUpdateCheckAsync();
        }

        private void OnUpdateStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(AppUpdateState.CanUpdate) or nameof(AppUpdateState.RollBackUpdateAvailable))
                _dispatcher.BeginInvoke(RefreshRecoveryHint);
        }

        private void RefreshRecoveryHint()
        {
            bool nothingToRecoverWith = !UpdateState.CanUpdate && !UpdateState.RollBackUpdateAvailable;
            NoRecoveryHint.Visibility = nothingToRecoverWith ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task AutoClose(int autoCloseinMiliSeconds)
        {
            await Task.Delay(autoCloseinMiliSeconds);
            Close();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        { _nav.OpenUrl(Core.Constants.DiscordInviteUrl); }

        private void Github_Click(object sender, RoutedEventArgs e)
        { _nav.OpenUrl(Core.Constants.GitHubNewIssueUrl); }

        private void SupportLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
                _nav.OpenUrl(e.Uri.AbsoluteUri);

            e.Handled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            _nav.OpenFolder(_env.LogPath);
        }

        private void OpenCurrentLog_Click(object sender, RoutedEventArgs e)
        {
            string? currentLogPath = ResolveCurrentLogPath();
            if (!string.IsNullOrWhiteSpace(currentLogPath) && _nav.OpenFileInExplorer(currentLogPath))
                return;

            _nav.OpenFolder(_env.LogPath);
        }

        private UpdateApp CreateUpdateApp(bool createNewAppLocation = false) =>
            new UpdateApp(UpdateState, _httpClientFactory, _dispatcher, createNewAppLocation);

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            CreateUpdateApp(true).SelectCustomZip();
        }

        private void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateState.CanUpdate)
            {
                _nav.OpenUrl(Core.Constants.GitHubReleasesPageUrl);
                return;
            }

            UpdateState.CanUpdate = false;
            UpdateState.CanUpdateLabel = false;
            var updateApp = CreateUpdateApp(true);
            Task.Run(() => updateApp.PrepareUpdate());
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            string report = Core.Diagnostics.CrashReport.Format(
                UpdateState.AppVersion?.VersionNumber,
                _exception.Message,
                _exception.StackTrace,
                ResolveCurrentLogPath(),
                RuntimeInformation.OSDescription,
                _occurredAt);

            try
            {
                Clipboard.SetText(report);
                CopyDetailsLabel.Text = "Copied";
                _ = ResetCopyLabel();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Could not copy the crash details to the clipboard: {ex.Message}");
                CopyDetailsLabel.Text = "Copy failed";
                _ = ResetCopyLabel();
            }
        }

        private async Task ResetCopyLabel()
        {
            await Task.Delay(2000);
            CopyDetailsLabel.Text = "Copy details";
        }

        private async Task ManualUpdateCheckAsync()
        {
            try
            {
                var updateCheckTask = _versionService.CheckForUpdateAndWait(true);
                var delayTask = Task.Delay(Core.Constants.ManualUpdateCheckTimeout);
                await Task.WhenAny(updateCheckTask, delayTask);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"The update check from the error dialog failed: {ex.Message}");
            }
        }

        private void rollback_Click(object sender, RoutedEventArgs e)
        {
            CreateUpdateApp(true).StartRollback();
        }

        private string? ResolveCurrentLogPath()
        {
            if (string.IsNullOrWhiteSpace(_env.LogPath) || !Directory.Exists(_env.LogPath))
                return null;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string[] preferredPaths =
            {
                Path.Combine(_env.LogPath, $"{today}.log"),
                Path.Combine(_env.LogPath, $"errors-{today}.log"),
                Path.Combine(_env.LogPath, "startup-early.log")
            };

            foreach (string path in preferredPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return new DirectoryInfo(_env.LogPath)
                .EnumerateFiles()
                .Where(file =>
                    file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                    file.Name.Contains(".log.", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
    }
}
