using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Toast;

public sealed class ToastService : IToastService
{
    /// <summary>Duration (ms) of the exit animation — must match the XAML storyboard duration.</summary>
    public const int ExitAnimationMs = 220;

    private const int MaxToasts = 4;

    private readonly IUiDispatcher _ui;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly ITrayIconService _trayIconService;

    public ToastService(
        IUiDispatcher ui,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ITrayIconService trayIconService)
    {
        _ui = ui;
        _appSettingsProvider = appSettingsProvider;
        _trayIconService = trayIconService;
    }

    public ObservableCollection<ToastItemViewModel> Toasts { get; } = new();

    public void Show(
        string title,
        string message,
        ToastType type = ToastType.Info,
        ToastAction? action = null,
        int durationMs = 5000,
        string? key = null)
    {
        _ui.BeginInvoke(() =>
        {
            // Silently replace any existing toast with the same key (no animation — just remove)
            if (key != null)
            {
                for (int i = Toasts.Count - 1; i >= 0; i--)
                {
                    if (Toasts[i].Key == key)
                    {
                        Toasts[i].MarkDismissed();
                        Toasts.RemoveAt(i);
                    }
                }
            }

            // Instant eviction when at cap — avoid spawning exit-animation timers under rapid calls
            while (Toasts.Count >= MaxToasts)
            {
                Toasts[0].MarkDismissed();
                Toasts.RemoveAt(0);
            }

            var item = new ToastItemViewModel(title, message, type, action, this, key);
            Toasts.Add(item);
            ShowTrayNotificationWhenUnfocused(title, message, action);

            var autoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(durationMs, ExitAnimationMs + 100))
            };
            autoTimer.Tick += (_, _) =>
            {
                autoTimer.Stop();
                Dismiss(item);
            };
            autoTimer.Start();
        });
    }

    private void ShowTrayNotificationWhenUnfocused(string title, string message, ToastAction? action)
    {
        AppSettings appSettings = _appSettingsProvider.Value;
        if (!appSettings.EnableTrayNotifications || !_trayIconService.IsInitialized)
            return;

        MainWindow? mainWindow = App.mainWindow;
        bool appIsFocused = mainWindow is { IsActive: true, IsVisible: true } &&
                            mainWindow.WindowState != WindowState.Minimized;

        if (appIsFocused)
            return;

        _trayIconService.Notify($"{title}{Environment.NewLine}{message}", action);
    }

    public void Dismiss(ToastItemViewModel item)
    {
        _ui.BeginInvoke(() =>
        {
            if (item.IsDismissed) return;
            item.MarkDismissed();
            item.IsExiting = true;

            var exitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ExitAnimationMs)
            };
            exitTimer.Tick += (_, _) =>
            {
                exitTimer.Stop();
                Toasts.Remove(item);
            };
            exitTimer.Start();
        });
    }
}
