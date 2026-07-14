using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    /// <summary>
    /// Interaction logic for ApplicationError.xaml
    /// </summary>
    public partial class ApplicationError : Window
    {
        public AppUpdateState UpdateState { get; }
        private readonly IEnvironmentService _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUiDispatcher _dispatcher;
        private readonly IVersionService _versionService;
        private readonly INavigationService _nav;

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
            MainError.Text = ex.Message;
            CallStack.Text = ex.StackTrace;
            if (autoclose)
                _ = AutoClose(autoCloseinMiliSeconds);
            CheckUpdateBtnn_Click(null, null);
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

        private void NewVersion_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (UpdateState.CanUpdate)
            {
                UpdateState.CanUpdate = false;
                UpdateState.CanUpdateLabel = false;
                var updateApp = CreateUpdateApp(true);
                Task.Run(() => updateApp.PrepareUpdate());
            }
            else
            {
                _nav.OpenUrl(Core.Constants.GitHubReleasesPageUrl);
            }
        }

        private async Task ManualUpdateCheckAsync()
        {
            var updateCheckTask = _versionService.CheckForUpdateAndWait(true);
            var delayTask = Task.Delay(Core.Constants.ManualUpdateCheckTimeout);
            await Task.WhenAny(updateCheckTask, delayTask);
        }

        private void CheckUpdateBtnn_Click(object sender, RoutedEventArgs e) { ManualUpdateCheckAsync(); }

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
