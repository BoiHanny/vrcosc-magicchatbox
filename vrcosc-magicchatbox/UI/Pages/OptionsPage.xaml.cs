using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
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

    private ContentControl? OptionsWrapper_Spotify;
    private ContentControl? OptionsWrapper_Lyrics;
    private ContentControl? OptionsWrapper_Twitch;
    private ContentControl? OptionsWrapper_TikTokLive;
    private ContentControl? OptionsWrapper_Discord;
    private ContentControl? OptionsWrapper_VrcRadar;
    private ContentControl? OptionsWrapper_Time;
    private ContentControl? OptionsWrapper_Weather;
    private ContentControl? OptionsWrapper_Pulsoid;
    private ContentControl? OptionsWrapper_ComponentStats;
    private ContentControl? OptionsWrapper_NetworkStatistics;
    private ContentControl? OptionsWrapper_WindowActivity;
    private ContentControl? OptionsWrapper_VrPerformance;
    private ContentControl? OptionsWrapper_TrackerBattery;
    private ContentControl? OptionsWrapper_OpenAI;
    private ContentControl? OptionsWrapper_Tts;
    private ContentControl? OptionsWrapper_AppOptions;
    private ContentControl? OptionsWrapper_Privacy;
    private ContentControl? OptionsWrapper_EggDev;
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
        if (Resources[key] is not DataTemplate template)
            return;

        if (template.LoadContent() is not FrameworkElement root)
            return;

        switch (key)
        {
            case "OptionsDeferredChunk1":
                OptionsWrapper_Spotify = root.FindName("OptionsWrapper_Spotify") as ContentControl;
                OptionsWrapper_Lyrics = root.FindName("OptionsWrapper_Lyrics") as ContentControl;
                OptionsWrapper_Twitch = root.FindName("OptionsWrapper_Twitch") as ContentControl;
                break;
            case "OptionsDeferredChunk2":
                OptionsWrapper_TikTokLive = root.FindName("OptionsWrapper_TikTokLive") as ContentControl;
                OptionsWrapper_Discord = root.FindName("OptionsWrapper_Discord") as ContentControl;
                OptionsWrapper_VrcRadar = root.FindName("OptionsWrapper_VrcRadar") as ContentControl;
                break;
            case "OptionsDeferredChunk3":
                OptionsWrapper_Time = root.FindName("OptionsWrapper_Time") as ContentControl;
                OptionsWrapper_Weather = root.FindName("OptionsWrapper_Weather") as ContentControl;
                OptionsWrapper_Pulsoid = root.FindName("OptionsWrapper_Pulsoid") as ContentControl;
                break;
            case "OptionsDeferredChunk4":
                OptionsWrapper_ComponentStats = root.FindName("OptionsWrapper_ComponentStats") as ContentControl;
                OptionsWrapper_NetworkStatistics = root.FindName("OptionsWrapper_NetworkStatistics") as ContentControl;
                OptionsWrapper_WindowActivity = root.FindName("OptionsWrapper_WindowActivity") as ContentControl;
                break;
            case "OptionsDeferredChunk5":
                OptionsWrapper_VrPerformance = root.FindName("OptionsWrapper_VrPerformance") as ContentControl;
                OptionsWrapper_TrackerBattery = root.FindName("OptionsWrapper_TrackerBattery") as ContentControl;
                OptionsWrapper_OpenAI = root.FindName("OptionsWrapper_OpenAI") as ContentControl;
                break;
            case "OptionsDeferredChunk6":
                OptionsWrapper_Tts = root.FindName("OptionsWrapper_Tts") as ContentControl;
                TtsOptionsSectionControl = root.FindName("TtsOptionsSectionControl") as TtsOptionsSection;
                OptionsWrapper_AppOptions = root.FindName("OptionsWrapper_AppOptions") as ContentControl;
                OptionsWrapper_Privacy = root.FindName("OptionsWrapper_Privacy") as ContentControl;
                PrivacySectionControl = root.FindName("PrivacySectionControl") as PrivacySection;
                break;
            case "OptionsDeferredChunk7":
                OptionsWrapper_EggDev = root.FindName("OptionsWrapper_EggDev") as ContentControl;
                break;
        }

        SectionsPanel.Children.Add(root);
        PlayChunkEntrance(root);
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
            ["Settings_VrcRadar"] = OptionsWrapper_VrcRadar!,
            ["Settings_HeartRate"] = OptionsWrapper_Pulsoid!,
            ["Settings_Time"] = OptionsWrapper_Time!,
            ["Settings_Weather"] = OptionsWrapper_Weather!,
            ["Settings_Twitch"] = OptionsWrapper_Twitch!,
            ["Settings_TikTokLive"] = OptionsWrapper_TikTokLive!,
            ["Settings_Discord"] = OptionsWrapper_Discord!,
            ["Settings_Spotify"] = OptionsWrapper_Spotify!,
            ["Settings_OpenAI"] = OptionsWrapper_OpenAI!,
            ["Settings_ComponentStats"] = OptionsWrapper_ComponentStats!,
            ["Settings_NetworkStatistics"] = OptionsWrapper_NetworkStatistics!,
            ["Settings_Chatting"] = OptionsWrapper_Chatting,
            ["Settings_TTS"] = OptionsWrapper_Tts!,
            ["Settings_MediaLink"] = OptionsWrapper_MediaLink,
            ["Settings_AppOptions"] = OptionsWrapper_AppOptions!,
            ["Settings_EggDev"] = OptionsWrapper_EggDev!,
            ["Settings_TrackerBattery"] = OptionsWrapper_TrackerBattery!,
            ["Settings_VrPerformance"] = OptionsWrapper_VrPerformance!,
            ["Settings_Lyrics"] = OptionsWrapper_Lyrics!,
            ["Settings_Privacy"] = OptionsWrapper_Privacy!,
            [MenuNavigationService.PrivacySoundpadTarget] = PrivacySectionControl!.SoundpadBridgeRow,
            ["Settings_WindowActivity"] = OptionsWrapper_WindowActivity!,
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
