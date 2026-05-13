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
            ["Settings_Status"] = OptionsWrapper_Status,
            ["Settings_VrcRadar"] = OptionsWrapper_VrcRadar,
            ["Settings_HeartRate"] = OptionsWrapper_Pulsoid,
            ["Settings_Time"] = OptionsWrapper_Time,
            ["Settings_Weather"] = OptionsWrapper_Weather,
            ["Settings_Twitch"] = OptionsWrapper_Twitch,
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
            ["Settings_Privacy"] = OptionsWrapper_Privacy,
            ["Settings_WindowActivity"] = OptionsWrapper_WindowActivity,
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
    {
        if (e.OriginalSource is not CheckBox)
            return;

        if (DataContext is OptionsPageViewModel vm)
            vm.OnSettingToggled();
    }

    public void SelectTTSOutput()
        => TtsOptionsSectionControl.SelectTTSOutput();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (DataContext is OptionsPageViewModel vm)
            vm.Navigation.OpenUrl(e.Uri.AbsoluteUri);

        e.Handled = true;
    }
}
