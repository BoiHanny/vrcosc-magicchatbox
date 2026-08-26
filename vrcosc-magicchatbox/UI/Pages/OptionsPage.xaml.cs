using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Controls;
using vrcosc_magicchatbox.UI.Pages.Options;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages;

public partial class OptionsPage : UserControl
{
    private const string ScrollMemoryKey = "options";

    private PrivacySection? PrivacySectionControl;
    private TtsOptionsSection? TtsOptionsSectionControl;

    private Dictionary<string, FrameworkElement>? _sectionMap;

    private OptionsPageViewModel? _attachedVm;

    private SectionRealizer? _realizer;

    public OptionsPage()
    {
        InitializeComponent();
        BuildSectionRealizer();

        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
            new RoutedEventHandler(OnSettingToggled));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
            new RoutedEventHandler(OnSettingToggled));

        DataContextChanged += OptionsPage_DataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OptionsPageViewModel vm && _attachedVm == null)
        {
            vm.ScrollToSectionRequested += OnScrollToSectionRequested;
            _attachedVm = vm;
        }

        _realizer?.Start();
        ScrollMemory.Restore(MainScroll, ScrollMemoryKey, () => _realizer?.RealizeVisible());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ScrollMemory.Detach(MainScroll);
        _realizer?.Stop();
        DetachViewModel();
    }

    private void DetachViewModel()
    {
        if (_attachedVm == null)
            return;

        _attachedVm.ScrollToSectionRequested -= OnScrollToSectionRequested;
        _attachedVm = null;
    }

    private void OptionsPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OptionsPageViewModel)
            DetachViewModel();

        if (e.NewValue is OptionsPageViewModel newVm && _attachedVm == null)
        {
            newVm.ScrollToSectionRequested += OnScrollToSectionRequested;
            _attachedVm = newVm;
        }
    }

    /// <summary>
    /// Registers all 23 sections in layout order. Nothing is constructed here: the realizer builds each
    /// section only when it approaches the viewport, which is what keeps a visit to Options cheap.
    /// </summary>
    private void BuildSectionRealizer()
    {
        // Falls back to a throwaway map when the container is not up, so the page still builds.
        var heights = App.Services?.GetService<ISettingsProvider<AppSettings>>()?.Value.OptionsSectionHeights
            ?? new Dictionary<string, double>();

        _realizer = new SectionRealizer(MainScroll, heights);

        // Chatting, Status and MediaLink stay declared inline in the XAML: they are the first thing on
        // screen, so deferring them would only trade build cost for a visible placeholder.
        _realizer.Add("spotify", OptionsWrapper_Spotify, () => new SpotifySection(), nameof(OptionsPageViewModel.SpotifySection));
        _realizer.Add("lyrics", OptionsWrapper_Lyrics, () => new LyricsSection(), nameof(OptionsPageViewModel.LyricsSection));
        _realizer.Add("twitch", OptionsWrapper_Twitch, () => new TwitchSection(), nameof(OptionsPageViewModel.TwitchSection));
        _realizer.Add("tiktoklive", OptionsWrapper_TikTokLive, () => new TikTokLiveSection(), nameof(OptionsPageViewModel.TikTokLiveSection));
        _realizer.Add("discord", OptionsWrapper_Discord, () => new DiscordSection(), nameof(OptionsPageViewModel.DiscordSection));
        _realizer.Add("vrcradar", OptionsWrapper_VrcRadar, () => new VrcRadarSection(), nameof(OptionsPageViewModel.VrcRadarSection));
        _realizer.Add("time", OptionsWrapper_Time, () => new TimeOptionsSection(), nameof(OptionsPageViewModel.TimeOptionsSection));
        _realizer.Add("weather", OptionsWrapper_Weather, () => new WeatherSection(), nameof(OptionsPageViewModel.WeatherSection));
        _realizer.Add("pulsoid", OptionsWrapper_Pulsoid, () => new PulsoidSection(), nameof(OptionsPageViewModel.PulsoidSection));
        _realizer.Add("componentstats", OptionsWrapper_ComponentStats, () => new ComponentStatsSection(), nameof(OptionsPageViewModel.ComponentStatsSection));
        _realizer.Add("networkstatistics", OptionsWrapper_NetworkStatistics, () => new NetworkStatisticsSection(), nameof(OptionsPageViewModel.NetworkStatisticsSection));
        _realizer.Add("windowactivity", OptionsWrapper_WindowActivity, () => new WindowActivitySection(), nameof(OptionsPageViewModel.WindowActivitySection));
        _realizer.Add("vrperformance", OptionsWrapper_VrPerformance, () => new VrPerformanceSection(), nameof(OptionsPageViewModel.VrPerformanceSection));
        _realizer.Add("trackerbattery", OptionsWrapper_TrackerBattery, () => new TrackerBatterySection(), nameof(OptionsPageViewModel.TrackerBatterySection));
        _realizer.Add("voicemod", OptionsWrapper_Voicemod, () => new VoicemodSection(), nameof(OptionsPageViewModel.VoicemodSection));
        _realizer.Add("openai", OptionsWrapper_OpenAI, () => new OpenAISection(), nameof(OptionsPageViewModel.OpenAISection));
        _realizer.Add("tts", OptionsWrapper_Tts, () => TtsOptionsSectionControl = new TtsOptionsSection(), nameof(OptionsPageViewModel.TtsSection));
        _realizer.Add("appoptions", OptionsWrapper_AppOptions, () => new AppOptionsSection(), nameof(OptionsPageViewModel.AppOptionsSection));
        _realizer.Add("privacy", OptionsWrapper_Privacy, () => PrivacySectionControl = new PrivacySection(), nameof(OptionsPageViewModel.PrivacySection));
        _realizer.Add("eggdev", OptionsWrapper_EggDev, () => new EggDevSection(), nameof(OptionsPageViewModel.EggDevSection));
    }

    private void EnsureSectionsRealized() => _realizer?.RealizeAll();

    /// <summary>Forces every section to exist so a scripted run has something to click.</summary>
    internal void RealizeAllSectionsForDiagnostics() => EnsureSectionsRealized();

    private void EnsureSectionMap()
    {
        EnsureSectionsRealized();

        _sectionMap ??= new Dictionary<string, FrameworkElement>
        {
            ["Settings_Status"] = OptionsWrapper_Status,
            ["Settings_VrcRadar"] = OptionsWrapper_VrcRadar,
            ["Settings_HeartRate"] = OptionsWrapper_Pulsoid,
            ["Settings_Time"] = OptionsWrapper_Time,
            ["Settings_Weather"] = OptionsWrapper_Weather,
            ["Settings_Twitch"] = OptionsWrapper_Twitch,
            ["Settings_TikTokLive"] = OptionsWrapper_TikTokLive,
            ["Settings_Discord"] = OptionsWrapper_Discord,
            ["Settings_Spotify"] = OptionsWrapper_Spotify,
            ["Settings_OpenAI"] = OptionsWrapper_OpenAI,
            ["Settings_Voicemod"] = OptionsWrapper_Voicemod,
            ["Settings_ComponentStats"] = OptionsWrapper_ComponentStats,
            ["Settings_NetworkStatistics"] = OptionsWrapper_NetworkStatistics,
            ["Settings_Chatting"] = OptionsWrapper_Chatting,
            ["Settings_TTS"] = OptionsWrapper_Tts,
            ["Settings_MediaLink"] = OptionsWrapper_MediaLink,
            ["Settings_AppOptions"] = OptionsWrapper_AppOptions,
            ["Settings_EggDev"] = OptionsWrapper_EggDev,
            ["Settings_TrackerBattery"] = OptionsWrapper_TrackerBattery,
            ["Settings_VrPerformance"] = OptionsWrapper_VrPerformance,
            ["Settings_Lyrics"] = OptionsWrapper_Lyrics,
            ["Settings_Privacy"] = OptionsWrapper_Privacy,
            [MenuNavigationService.PrivacySoundpadTarget] = PrivacySectionControl!.SoundpadBridgeRow,
            ["Settings_WindowActivity"] = OptionsWrapper_WindowActivity,
        };
    }

    private void OnScrollToSectionRequested(string settingName)
    {
        EnsureSectionMap();
        if (_sectionMap != null && _sectionMap.TryGetValue(settingName, out var section))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                section.BringIntoView();

                if (MainScroll.ActualHeight == 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                    {
                        section.BringIntoView();
                    });
                }
            });
        }
    }

    private void OnSettingToggled(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not CheckBox)
            return;

        if (DataContext is OptionsPageViewModel vm)
            vm.OnSettingToggled();
    }

    public void SelectTTSOutput()
    {
        EnsureSectionsRealized();
        TtsOptionsSectionControl?.SelectTTSOutput();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (DataContext is OptionsPageViewModel vm)
            vm.Navigation.OpenUrl(e.Uri.AbsoluteUri);

        e.Handled = true;
    }
}
