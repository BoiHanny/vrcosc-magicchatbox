using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Messaging;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly ChatStatusDisplayState _chatStatus;
        private readonly Lazy<OSCController> _osc;
        private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
        private readonly IOptionsSectionResetService _sectionReset;
        private readonly IToastService _toast;

        public WindowActivitySectionViewModel WindowActivitySection { get; }
        public MediaLinkSectionViewModel MediaLinkSection { get; }
        public WeatherSectionViewModel WeatherSection { get; }
        public TwitchSectionViewModel TwitchSection { get; }
        public TikTokLiveSectionViewModel TikTokLiveSection { get; }
        public DiscordSectionViewModel DiscordSection { get; }
        public SpotifySectionViewModel SpotifySection { get; }
        public TrackerBatterySectionViewModel TrackerBatterySection { get; }
        public VrPerformanceSectionViewModel VrPerformanceSection { get; }
        public LyricsSectionViewModel LyricsSection { get; }
        public PulsoidSectionViewModel PulsoidSection { get; }
        public OpenAISectionViewModel OpenAISection { get; }
        public TtsSectionViewModel TtsSection { get; }
        public TimeOptionsSectionViewModel TimeOptionsSection { get; }
        public NetworkStatisticsSectionViewModel NetworkStatisticsSection { get; }
        public ChattingOptionsSectionViewModel ChattingOptionsSection { get; }
        public ComponentStatsSectionViewModel ComponentStatsSection { get; }
        public StatusSectionViewModel StatusSection { get; }
        public AppOptionsSectionViewModel AppOptionsSection { get; }
        public EggDevSectionViewModel EggDevSection { get; }
        public PrivacySectionViewModel PrivacySection { get; }
        public VrcRadarSectionViewModel VrcRadarSection { get; }
        public VoicemodSectionViewModel VoicemodSection { get; }
        public INavigationService Navigation { get; }

        public OptionsPageViewModel(
            ChatStatusDisplayState chatStatus,
            Lazy<OSCController> osc,
            ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
            INavigationService nav,
            IOptionsSectionResetService sectionReset,
            IToastService toast,
            WindowActivitySectionViewModel windowActivitySection,
            MediaLinkSectionViewModel mediaLinkSection,
            WeatherSectionViewModel weatherSection,
            TwitchSectionViewModel twitchSection,
            TikTokLiveSectionViewModel tikTokLiveSection,
            DiscordSectionViewModel discordSection,
            SpotifySectionViewModel spotifySection,
            TrackerBatterySectionViewModel trackerBatterySection,
            VrPerformanceSectionViewModel vrPerformanceSection,
            LyricsSectionViewModel lyricsSection,
            PulsoidSectionViewModel pulsoidSection,
            OpenAISectionViewModel openAISection,
            TtsSectionViewModel ttsSection,
            TimeOptionsSectionViewModel timeOptionsSection,
            NetworkStatisticsSectionViewModel networkStatisticsSection,
            ChattingOptionsSectionViewModel chattingOptionsSection,
            ComponentStatsSectionViewModel componentStatsSection,
            StatusSectionViewModel statusSection,
            AppOptionsSectionViewModel appOptionsSection,
            EggDevSectionViewModel eggDevSection,
            PrivacySectionViewModel privacySection,
            VrcRadarSectionViewModel vrcRadarSection,
            VoicemodSectionViewModel voicemodSection)
        {
            _chatStatus = chatStatus;
            _osc = osc;
            _integrationSettingsProvider = integrationSettingsProvider;
            Navigation = nav;
            _sectionReset = sectionReset;
            _toast = toast;

            WindowActivitySection = windowActivitySection;
            MediaLinkSection = mediaLinkSection;
            WeatherSection = weatherSection;
            TwitchSection = twitchSection;
            TikTokLiveSection = tikTokLiveSection;
            DiscordSection = discordSection;
            SpotifySection = spotifySection;
            TrackerBatterySection = trackerBatterySection;
            VrPerformanceSection = vrPerformanceSection;
            LyricsSection = lyricsSection;
            PulsoidSection = pulsoidSection;
            OpenAISection = openAISection;
            TtsSection = ttsSection;
            TimeOptionsSection = timeOptionsSection;
            NetworkStatisticsSection = networkStatisticsSection;
            ChattingOptionsSection = chattingOptionsSection;
            ComponentStatsSection = componentStatsSection;
            StatusSection = statusSection;
            AppOptionsSection = appOptionsSection;
            EggDevSection = eggDevSection;
            PrivacySection = privacySection;
            VrcRadarSection = vrcRadarSection;
            VoicemodSection = voicemodSection;
        }

        public void OnSettingToggled()
        {
            if (!_chatStatus.ScanPause)
                _osc.Value.BuildOSC(allowExternalRefresh: false);
            _integrationSettingsProvider.Save();
        }

        private readonly ReplayableRequest<string> _scrollToSection = new();

        public event Action<string>? ScrollToSectionRequested
        {
            add => _scrollToSection.Requested += value;
            remove => _scrollToSection.Requested -= value;
        }

        public void RequestScrollToSection(string settingName) => _scrollToSection.Raise(settingName);

        [RelayCommand]
        private async Task ResetSectionAsync(string? sectionKey)
        {
            if (string.IsNullOrWhiteSpace(sectionKey))
                return;

            bool confirmed = ConfirmationDialog.Show(
                "Reset section",
                "Reset this section to default settings?",
                "Saved tokens, client IDs and the integration's on/off switch are kept. A module that is running is stopped, reset, and started again.",
                "Reset");

            if (!confirmed)
                return;

            var reset = await _sectionReset.ResetSectionAsync(sectionKey).ConfigureAwait(true);
            if (!_chatStatus.ScanPause)
                _osc.Value.BuildOSC(allowExternalRefresh: false);

            var message = reset.Note is null
                ? $"{reset.ResetCount} setting(s) reset."
                : $"{reset.ResetCount} setting(s) reset. {reset.Note}";
            _toast.Show(reset.DisplayName, message, ToastType.Success, key: $"reset-{sectionKey}");
        }
    }
}
