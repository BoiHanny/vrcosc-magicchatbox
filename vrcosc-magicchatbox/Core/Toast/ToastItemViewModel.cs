using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows.Media;

namespace vrcosc_magicchatbox.Core.Toast;

public partial class ToastItemViewModel : ObservableObject
{
    private readonly IToastService _service;

    public string Title { get; }
    public string Message { get; }
    public ToastType Type { get; }
    public ToastAction? Action { get; }

    /// <summary>Optional deduplication key — a new Show() with the same key silently replaces the existing one.</summary>
    public string? Key { get; }

    internal bool IsDismissed { get; private set; }

    [ObservableProperty]
    private bool _isExiting;

    public bool HasAction => Action is not null;

    public SolidColorBrush AccentBrush => Type switch
    {
        ToastType.Success => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        ToastType.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
        ToastType.Error => new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
        ToastType.Privacy => new SolidColorBrush(Color.FromRgb(0xC3, 0xA9, 0xFF)),
        _ => new SolidColorBrush(Color.FromRgb(0x31, 0xB7, 0xB4)), // Info = teal
    };

    public string TypeIcon => Type switch
    {
        ToastType.Success => "✅",
        ToastType.Warning => "⚠️",
        ToastType.Error => "❌",
        ToastType.Privacy => "🔒",
        _ => "ℹ️",
    };

    public ToastItemViewModel(string title, string message, ToastType type, ToastAction? action, IToastService service, string? key = null)
    {
        Title = title;
        Message = message;
        Type = type;
        Action = action;
        Key = key;
        _service = service;
    }

    internal void MarkDismissed() => IsDismissed = true;

    [RelayCommand]
    private void Dismiss() => _service.Dismiss(this);

    [RelayCommand]
    private async Task ExecuteAction()
    {
        if (Action is not null)
            await Action.Execute();
        _service.Dismiss(this);
    }
}
