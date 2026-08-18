using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{

    public partial class ViewModel : ObservableObject, Core.State.IAppState
    {
        public AppSettings AppSettingsInstance { get; }
        public TtsSettings TtsSettingsInstance { get; }
        public TimeSettings TimeSettingsInstance { get; }

        private readonly Lazy<IOscSender> _oscSender;
        private readonly IMenuNavigationService _menuNav;
        private readonly IVersionService _versionService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUiDispatcher _dispatcher;
        private readonly INavigationService _nav;
        private readonly Lazy<ScanLoopService> _scanLoop;

        public AppUpdateState UpdateState { get; }
        public OscDisplayState OscDisplay { get; }
        public TtsAudioDisplayState TtsAudio { get; }

        public EmojiService Emojis { get; }

        private readonly PulsoidDisplayState _pulsoid;

        private readonly Lazy<IModuleHost> _modules;
        public IModuleHost Modules => _modules.Value;

        public ChatSettings ChatSettings { get; }

        [ObservableProperty] private bool _BussyBoysMode = false;
        [ObservableProperty] private bool _Egg_Dev = false;
        [ObservableProperty] private int _MainWindowBlurEffect = 0;

        private readonly Lazy<ChattingPageViewModel> _chatting;
        public ChattingPageViewModel Chatting => _chatting.Value;
        private readonly Lazy<StatusPageViewModel> _status;
        public StatusPageViewModel Status => _status.Value;
        private readonly Lazy<IntegrationsPageViewModel> _integrations;
        public IntegrationsPageViewModel Integrations => _integrations.Value;
        private readonly Lazy<OptionsPageViewModel> _options;
        public OptionsPageViewModel Options => _options.Value;
        private readonly Lazy<StatusSetSwitcherViewModel> _statusSets;
        public StatusSetSwitcherViewModel StatusSets => _statusSets.Value;
        private readonly Lazy<AfkStyleViewModel> _afkStyles;
        public AfkStyleViewModel AfkStyles => _afkStyles.Value;

        public ViewModel(
            AppUpdateState updateState,
            OscDisplayState oscDisplay,
            TtsAudioDisplayState ttsAudio,
            PulsoidDisplayState pulsoid,
            EmojiService emojis,
            IAppInfoService appInfoService,
            ISettingsProvider<AppSettings> appSettingsProvider,
            ISettingsProvider<TimeSettings> timeSettingsProvider,
            ISettingsProvider<TtsSettings> ttsSettingsProvider,
            ISettingsProvider<ChatSettings> chatSettingsProvider,
            Lazy<IOscSender> oscSender,
            IMenuNavigationService menuNav,
            IVersionService versionService,
            IHttpClientFactory httpClientFactory,
            IUiDispatcher dispatcher,
            INavigationService nav,
            Lazy<ScanLoopService> scanLoop,
            Lazy<IModuleHost> modules,
            Lazy<ChattingPageViewModel> chatting,
            Lazy<StatusPageViewModel> status,
            Lazy<IntegrationsPageViewModel> integrations,
            Lazy<OptionsPageViewModel> options,
            Lazy<StatusSetSwitcherViewModel> statusSets,
            Lazy<AfkStyleViewModel> afkStyles)
        {
            UpdateState = updateState;
            OscDisplay = oscDisplay;
            TtsAudio = ttsAudio;
            _pulsoid = pulsoid;
            Emojis = emojis;

            AppSettingsInstance = appSettingsProvider.Value;
            TtsSettingsInstance = ttsSettingsProvider.Value;
            TimeSettingsInstance = timeSettingsProvider.Value;
            ChatSettings = chatSettingsProvider.Value;

            _oscSender = oscSender;
            _menuNav = menuNav;
            _versionService = versionService;
            _httpClientFactory = httpClientFactory;
            _dispatcher = dispatcher;
            _nav = nav;
            _scanLoop = scanLoop;

            _modules = modules;
            _chatting = chatting;
            _status = status;
            _integrations = integrations;
            _options = options;
            _statusSets = statusSets;
            _afkStyles = afkStyles;

            _selectedMenuIndex = AppSettingsInstance.CurrentMenuItem is >= 0 and <= 3
                ? AppSettingsInstance.CurrentMenuItem
                : 3;

            UpdateState.AppVersion = new Models.Version(appInfoService.GetApplicationVersion());

            Emojis.ShuffleEmojis();
            Emojis.CurrentEmoji = Emojis.GetNextEmoji();
        }

        [RelayCommand]
        private void ToggleVoice()
            => _oscSender.Value.ToggleVoice(true);

        [RelayCommand]
        private void ActivateSetting(string? settingName)
        {
            if (string.IsNullOrWhiteSpace(settingName)) return;
            _menuNav.ActivateSetting(settingName);
        }

        [RelayCommand]
        private void ChangeMenu(string s)
        {
            if (int.TryParse(s, out int index))
                _menuNav.NavigateToPage(index);
        }

        [RelayCommand]
        private void NavigateBack() => _menuNav.NavigateBack();

        [RelayCommand]
        private void NavigateForward() => _menuNav.NavigateForward();

        [RelayCommand]
        private void OpenDiscord() => _nav.OpenUrl(Core.Constants.DiscordInviteUrl);

        [RelayCommand]
        private void OpenGitHub() => _nav.OpenUrl(Core.Constants.GitHubRepoUrl);

        [RelayCommand]
        private void OpenWiki() => _nav.OpenUrl(Core.Constants.WikiHomeUrl);

        [RelayCommand]
        private void OpenGitHubChanges()
        {
            var url = string.IsNullOrWhiteSpace(UpdateState.TagURL)
                ? Core.Constants.GitHubReleasesPageUrl
                : UpdateState.TagURL;
            _nav.OpenUrl(url);
        }

        [RelayCommand]
        private async Task CheckUpdate()
        {
            var updateCheckTask = _versionService.CheckForUpdateAndWait(true);
            var delayTask = Task.Delay(Core.Constants.ManualUpdateCheckTimeout);
            await Task.WhenAny(updateCheckTask, delayTask);
        }

        [RelayCommand]
        private void StartUpdate()
        {
            if (AppSettingsInstance.UseCustomProfile)
            {
                Logging.WriteException(new Exception("Cannot update while using a custom profile."), MSGBox: true);
                return;
            }

            if (UpdateState.CanUpdate)
            {
                UpdateState.CanUpdate = false;
                UpdateState.CanUpdateLabel = false;
                var updateApp = new UpdateApp(
                    UpdateState,
                    _httpClientFactory,
                    _dispatcher,
                    true);
                Task.Run(() => updateApp.PrepareUpdate());
            }
            else
            {
                _nav.OpenUrl(Core.Constants.GitHubReleasesPageUrl);
            }
        }

        [RelayCommand]
        private void OnMasterSwitchToggled()
        {
            if (MasterSwitch)
            {
                _scanLoop.Value.Start();
            }
            else
            {
                _scanLoop.Value.Stop();
                _ = ObserveFireAndForget(_oscSender.Value.SentClearMessage(1000));
            }
        }

        private static async Task ObserveFireAndForget(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void HandleMasterSwitchToggled()
            => OnMasterSwitchToggled();


        public bool PulsoidAuthConnected
        {
            get => _pulsoid.AuthConnected;
            set
            {
                if (_pulsoid.AuthConnected != value)
                {
                    _pulsoid.AuthConnected = value;
                    NotifyPropertyChanged(nameof(PulsoidAuthConnected));
                    NotifyPropertyChanged(nameof(PulsoidAuthState));
                }
            }
        }

        public ViewModels.State.PulsoidAuthState PulsoidAuthState
        {
            get => _pulsoid.AuthState;
            set
            {
                if (_pulsoid.AuthState != value)
                {
                    bool connectedBefore = _pulsoid.AuthConnected;
                    _pulsoid.AuthState = value;
                    NotifyPropertyChanged(nameof(PulsoidAuthState));
                    if (connectedBefore != _pulsoid.AuthConnected)
                        NotifyPropertyChanged(nameof(PulsoidAuthConnected));
                }
            }
        }

        #region Properties

        [ObservableProperty]
        private bool _IsVRRunning = false;
        [ObservableProperty]
        private bool _MasterSwitch = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsUiObservable))]
        private bool _IsWindowOnScreen = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsUiObservable))]
        private bool _IsTrayMenuOpen = false;

        public bool IsUiObservable => IsWindowOnScreen || IsTrayMenuOpen;

        private int _selectedMenuIndex = 3;

        public int SelectedMenuIndex
        {
            get => _selectedMenuIndex;
            set
            {
                if (_selectedMenuIndex != value)
                {
                    _selectedMenuIndex = value;
                    AppSettingsInstance.CurrentMenuItem = value;
                    NotifyPropertyChanged(nameof(SelectedMenuIndex));
                }
            }
        }
        #endregion

        #region PropChangedEvent
        private void NotifyPropertyChanged(string name)
        { OnPropertyChanged(new PropertyChangedEventArgs(name)); }
        #endregion
    }
}
