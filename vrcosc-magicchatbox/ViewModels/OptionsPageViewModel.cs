using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{
    /// <summary>
    /// Page-specific ViewModel for the Options page.
    /// Owns only the cross-cutting setting-toggle broadcast.
    /// All section-specific logic is delegated to section ViewModels.
    /// </summary>
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
        public DiscordSectionViewModel DiscordSection { get; }
        public SpotifySectionViewModel SpotifySection { get; }
        public TrackerBatterySectionViewModel TrackerBatterySection { get; }
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

        public INavigationService Navigation { get; }

        /// <summary>
        /// Initializes the options page ViewModel, receiving all section ViewModels
        /// and shared services via dependency injection.
        /// </summary>
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
            DiscordSectionViewModel discordSection,
            SpotifySectionViewModel spotifySection,
            TrackerBatterySectionViewModel trackerBatterySection,
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
            VrcRadarSectionViewModel vrcRadarSection)
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
            DiscordSection = discordSection;
            SpotifySection = spotifySection;
            TrackerBatterySection = trackerBatterySection;
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
        }

        /// <summary>
        /// Called when any settings toggle changes. Rebuilds OSC and saves settings.
        /// </summary>
        public void OnSettingToggled()
        {
            if (!_chatStatus.ScanPause)
                _osc.Value.BuildOSC();
            _integrationSettingsProvider.Save();
        }

        /// <summary>Raised when ActivateSetting opens a section and the view should scroll to it.</summary>
        public event Action<string>? ScrollToSectionRequested;

        /// <summary>Request the Options page to scroll to the named setting section.</summary>
        public void RequestScrollToSection(string settingName)
            => ScrollToSectionRequested?.Invoke(settingName);

        [RelayCommand]
        private async Task ResetSectionAsync(string? sectionKey)
        {
            if (string.IsNullOrWhiteSpace(sectionKey))
                return;

            bool confirmed = ConfirmationDialog.Show(
                "Reset section",
                "Reset this section to default settings?",
                "Saved tokens and client IDs are preserved where possible. Running modules are restarted only when the reset service can do that safely.",
                "Reset");

            if (!confirmed)
                return;

            var reset = await _sectionReset.ResetSectionAsync(sectionKey).ConfigureAwait(true);
            if (!_chatStatus.ScanPause)
                _osc.Value.BuildOSC();

            var message = reset.Note is null
                ? $"{reset.ResetCount} setting(s) reset."
                : $"{reset.ResetCount} setting(s) reset. {reset.Note}";
            _toast.Show(reset.DisplayName, message, ToastType.Success, key: $"reset-{sectionKey}");
        }
    }
}
