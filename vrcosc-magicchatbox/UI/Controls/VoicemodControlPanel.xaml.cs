using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Controls;

public partial class VoicemodControlPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(VoicemodSectionViewModel),
        typeof(VoicemodControlPanel),
        new PropertyMetadata(null, OnViewModelChanged));

    private bool _bleepHeld;
    private Window? _ownerWindow;

    public VoicemodSectionViewModel? ViewModel
    {
        get => (VoicemodSectionViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public VoicemodControlPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void BleepButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
            button.CaptureMouse();

        HoldBleep();
        e.Handled = true;
    }

    private void BleepButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseBleep();
        if (sender is Button button && button.IsMouseCaptured)
            button.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void BleepButton_LostMouseCapture(object sender, MouseEventArgs e) => ReleaseBleep();

    private void BleepButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ReleaseBleep();

    private void BleepButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter) || e.IsRepeat)
            return;

        HoldBleep();
        e.Handled = true;
    }

    private void BleepButton_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter))
            return;

        ReleaseBleep();
        e.Handled = true;
    }

    private void HoldBleep()
    {
        if (_bleepHeld)
            return;

        _bleepHeld = true;
        if (ViewModel?.BeginBleepCommand.CanExecute(null) == true)
            ViewModel.BeginBleepCommand.Execute(null);
    }

    private void ReleaseBleep()
    {
        if (!_bleepHeld)
            return;

        _bleepHeld = false;
        if (ViewModel?.EndBleepCommand.CanExecute(null) == true)
            ViewModel.EndBleepCommand.Execute(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Window? window = Window.GetWindow(this);
        if (ReferenceEquals(window, _ownerWindow))
            return;

        DetachWindow();
        _ownerWindow = window;
        if (_ownerWindow != null)
            _ownerWindow.Deactivated += OwnerWindow_Deactivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReleaseBleep();
        DetachWindow();
    }

    private void OwnerWindow_Deactivated(object? sender, EventArgs e) => ReleaseBleep();

    private void DetachWindow()
    {
        if (_ownerWindow != null)
            _ownerWindow.Deactivated -= OwnerWindow_Deactivated;
        _ownerWindow = null;
    }

    private static void OnViewModelChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is VoicemodControlPanel panel)
        {
            panel.DataContext = e.NewValue;
            panel.PanelContent.DataContext = e.NewValue;
            panel.VoiceAndSwitchCards.DataContext = e.NewValue;
            panel.SoundboardCard.DataContext = e.NewValue;
            panel.ParametersCard.DataContext = e.NewValue;
        }
    }
}
