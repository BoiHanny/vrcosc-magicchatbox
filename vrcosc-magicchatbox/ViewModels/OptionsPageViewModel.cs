using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
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

        public WindowActivitySectionViewModel WindowActivitySection { get; }
        public MediaLinkSectionViewModel MediaLinkSection { get; }
        public WeatherSectionViewModel WeatherSection { get; }
        public TwitchSectionViewModel TwitchSection { get; }
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
            WindowActivitySectionViewModel windowActivitySection,
            MediaLinkSectionViewModel mediaLinkSection,
            WeatherSectionViewModel weatherSection,
            TwitchSectionViewModel twitchSection,
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
            PrivacySectionViewModel privacySection)
        {
            _chatStatus = chatStatus;
            _osc = osc;
            _integrationSettingsProvider = integrationSettingsProvider;
            Navigation = nav;

            WindowActivitySection = windowActivitySection;
            MediaLinkSection = mediaLinkSection;
            WeatherSection = weatherSection;
            TwitchSection = twitchSection;
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
    }
}
