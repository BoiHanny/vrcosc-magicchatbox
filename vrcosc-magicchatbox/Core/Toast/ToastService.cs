using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Core.Toast;

public sealed class ToastService : IToastService
{
    /// <summary>Duration (ms) of the exit animation — must match the XAML storyboard duration.</summary>
    public const int ExitAnimationMs = 220;

    private const int MaxToasts = 4;

    private readonly IUiDispatcher _ui;

    public ObservableCollection<ToastItemViewModel> Toasts { get; } = new();

    public ToastService(IUiDispatcher ui) => _ui = ui;

    public void Show(
        string title,
        string message,
        ToastType type = ToastType.Info,
        ToastAction? action = null,
        int durationMs = 5000,
        string? key = null)
    {
        _ui.Invoke(() =>
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

    public void Dismiss(ToastItemViewModel item)
    {
        _ui.Invoke(() =>
        {
            if (item.IsDismissed) return;
            item.MarkDismissed();
            item.IsExiting = true;

            // Remove from collection after the exit animation completes
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
