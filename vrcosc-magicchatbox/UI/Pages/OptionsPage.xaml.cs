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
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Pages.Options;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages;

public partial class OptionsPage : UserControl
{
    private static readonly string[] DeferredChunkKeys =
    {
        "OptionsDeferredChunk1",
        "OptionsDeferredChunk2",
        "OptionsDeferredChunk3",
        "OptionsDeferredChunk4",
        "OptionsDeferredChunk5",
        "OptionsDeferredChunk6",
        "OptionsDeferredChunk7",
    };

    private static readonly TimeSpan ChunkTickBudget = TimeSpan.FromMilliseconds(6);

    private readonly Queue<string> _pendingChunkKeys = new(DeferredChunkKeys);

    private PrivacySection? PrivacySectionControl;
    private TtsOptionsSection? TtsOptionsSectionControl;

    private Dictionary<string, FrameworkElement>? _sectionMap;

    private OptionsPageViewModel? _attachedVm;

    private bool _chunkQueued;

    public OptionsPage()
    {
        InitializeComponent();

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

        QueueNextChunk();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachViewModel();

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

    private void QueueNextChunk()
    {
        if (_chunkQueued || _pendingChunkKeys.Count == 0)
            return;

        _chunkQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(LoadNextChunk));
    }

    private void LoadNextChunk()
    {
        _chunkQueued = false;

        if (_pendingChunkKeys.Count == 0 || !IsLoaded)
            return;

        var tick = Stopwatch.StartNew();
        do
        {
            LoadChunk(_pendingChunkKeys.Dequeue());
        }
        while (_pendingChunkKeys.Count > 0 && tick.Elapsed < ChunkTickBudget);

        QueueNextChunk();
    }

    private void EnsureSectionsRealized()
    {
        while (_pendingChunkKeys.Count > 0)
            LoadChunk(_pendingChunkKeys.Dequeue());
    }

    private void LoadChunk(string key)
    {
        switch (key)
        {
            case "OptionsDeferredChunk1":
                Realize(OptionsWrapper_Spotify, new SpotifySection(), nameof(OptionsPageViewModel.SpotifySection));
                Realize(OptionsWrapper_Lyrics, new LyricsSection(), nameof(OptionsPageViewModel.LyricsSection));
                Realize(OptionsWrapper_Twitch, new TwitchSection(), nameof(OptionsPageViewModel.TwitchSection));
                break;
            case "OptionsDeferredChunk2":
                Realize(OptionsWrapper_TikTokLive, new TikTokLiveSection(), nameof(OptionsPageViewModel.TikTokLiveSection));
                Realize(OptionsWrapper_Discord, new DiscordSection(), nameof(OptionsPageViewModel.DiscordSection));
                Realize(OptionsWrapper_VrcRadar, new VrcRadarSection(), nameof(OptionsPageViewModel.VrcRadarSection));
                break;
            case "OptionsDeferredChunk3":
                Realize(OptionsWrapper_Time, new TimeOptionsSection(), nameof(OptionsPageViewModel.TimeOptionsSection));
                Realize(OptionsWrapper_Weather, new WeatherSection(), nameof(OptionsPageViewModel.WeatherSection));
                Realize(OptionsWrapper_Pulsoid, new PulsoidSection(), nameof(OptionsPageViewModel.PulsoidSection));
                break;
            case "OptionsDeferredChunk4":
                Realize(OptionsWrapper_ComponentStats, new ComponentStatsSection(), nameof(OptionsPageViewModel.ComponentStatsSection));
                Realize(OptionsWrapper_NetworkStatistics, new NetworkStatisticsSection(), nameof(OptionsPageViewModel.NetworkStatisticsSection));
                Realize(OptionsWrapper_WindowActivity, new WindowActivitySection(), nameof(OptionsPageViewModel.WindowActivitySection));
                break;
            case "OptionsDeferredChunk5":
                Realize(OptionsWrapper_VrPerformance, new VrPerformanceSection(), nameof(OptionsPageViewModel.VrPerformanceSection));
                Realize(OptionsWrapper_TrackerBattery, new TrackerBatterySection(), nameof(OptionsPageViewModel.TrackerBatterySection));
                Realize(OptionsWrapper_OpenAI, new OpenAISection(), nameof(OptionsPageViewModel.OpenAISection));
                break;
            case "OptionsDeferredChunk6":
                TtsOptionsSectionControl = new TtsOptionsSection();
                Realize(OptionsWrapper_Tts, TtsOptionsSectionControl, nameof(OptionsPageViewModel.TtsSection));
                Realize(OptionsWrapper_AppOptions, new AppOptionsSection(), nameof(OptionsPageViewModel.AppOptionsSection));
                PrivacySectionControl = new PrivacySection();
                Realize(OptionsWrapper_Privacy, PrivacySectionControl, nameof(OptionsPageViewModel.PrivacySection));
                break;
            case "OptionsDeferredChunk7":
                Realize(OptionsWrapper_EggDev, new EggDevSection(), nameof(OptionsPageViewModel.EggDevSection));
                break;
        }
    }

    private static void Realize(ContentControl wrapper, FrameworkElement content, string vmPropertyPath)
    {
        content.SetBinding(DataContextProperty, new Binding(vmPropertyPath));
        wrapper.Content = content;
        PlayChunkEntrance(content);
    }

    private static void PlayChunkEntrance(FrameworkElement chunk)
    {
        var slide = new TranslateTransform();
        chunk.RenderTransform = slide;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(160));

        chunk.BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, duration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = ease,
        });

        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, duration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = ease,
        });
    }

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
