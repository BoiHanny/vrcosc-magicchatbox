using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages;

/// <summary>Code-behind for the options page, wiring routed toggle events and delegating TTS output selection.</summary>
public partial class OptionsPage : UserControl
{
    private OptionsPageViewModel VM => (OptionsPageViewModel)DataContext;

    /// <summary>Maps AppSettings property names to the named section controls.</summary>
    private Dictionary<string, FrameworkElement>? _sectionMap;

    public OptionsPage()
    {
        InitializeComponent();

        // Handle all checkbox/toggle state changes via routed event bubbling
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
            new RoutedEventHandler(OnSettingToggled));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
            new RoutedEventHandler(OnSettingToggled));

        DataContextChanged += OptionsPage_DataContextChanged;
    }

    private void OptionsPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OptionsPageViewModel oldVm)
            oldVm.ScrollToSectionRequested -= OnScrollToSectionRequested;

        if (e.NewValue is OptionsPageViewModel newVm)
            newVm.ScrollToSectionRequested += OnScrollToSectionRequested;
    }

    private void EnsureSectionMap()
    {
        _sectionMap ??= new Dictionary<string, FrameworkElement>
        {
            ["Settings_Status"] = Section_Status,
            ["Settings_VrcRadar"] = Section_VrcRadar,
            ["Settings_HeartRate"] = Section_HeartRate,
            ["Settings_Time"] = Section_Time,
            ["Settings_Weather"] = Section_Weather,
            ["Settings_Twitch"] = Section_Twitch,
            ["Settings_Discord"] = Section_Discord,
            ["Settings_Spotify"] = Section_Spotify,
            ["Settings_OpenAI"] = Section_OpenAI,
            ["Settings_ComponentStats"] = Section_ComponentStats,
            ["Settings_NetworkStatistics"] = Section_NetworkStatistics,
            ["Settings_Chatting"] = Section_Chatting,
            ["Settings_TTS"] = TtsOptionsSectionControl,
            ["Settings_MediaLink"] = Section_MediaLink,
            ["Settings_AppOptions"] = Section_AppOptions,
            ["Settings_EggDev"] = Section_EggDev,
            ["Settings_TrackerBattery"] = Section_TrackerBattery,
            ["Settings_Privacy"] = Section_Privacy,
            ["Settings_WindowActivity"] = Section_WindowActivity,
        };
    }

    private void OnScrollToSectionRequested(string settingName)
    {
        EnsureSectionMap();
        if (_sectionMap != null && _sectionMap.TryGetValue(settingName, out var section))
        {
            // Wait for layout to complete, then scroll the section into view
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                section.BringIntoView();
            });
        }
    }

    private void OnSettingToggled(object sender, RoutedEventArgs e)
        => VM.OnSettingToggled();

    public void SelectTTSOutput()
        => TtsOptionsSectionControl.SelectTTSOutput();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        VM.Navigation.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
