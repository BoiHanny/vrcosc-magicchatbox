using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagicChatbox.Vocabulary;
using System;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.ViewModels.Avatar;

public partial class AvatarControlRowViewModel : ObservableObject
{
    private static readonly TimeSpan HoldWindow = TimeSpan.FromSeconds(2);

    private readonly IAvatarParameterSink _sink;
    private readonly Action<AvatarControlRowViewModel>? _pinChanged;
    private DateTime _heldUntilUtc = DateTime.MinValue;

    public string Name { get; }
    public string Leaf { get; }
    public SignalKind Kind { get; }
    public bool Writable { get; }
    public AvatarWidget Widget { get; }
    public bool IsBuiltIn { get; }

    [ObservableProperty] private double _value;
    [ObservableProperty] private bool _hasValue;
    [ObservableProperty] private bool _isHeld;
    [ObservableProperty] private bool _isPinned;

    public bool CanPin => !AvatarControlCatalog.IsInertOverOsc(Name);

    public bool IsToggle => Widget == AvatarWidget.Toggle;
    public bool IsSlider => Widget == AvatarWidget.Slider;
    public bool IsStepper => Widget == AvatarWidget.Stepper || Widget == AvatarWidget.Emote;
    public bool IsReadOnly => !Writable;

    public string StateWord => Kind switch
    {
        SignalKind.Bool => Value != 0 ? "yes" : "no",
        SignalKind.Int => ((int)Value).ToString(),
        _ => Value.ToString("0.##"),
    };

    public bool BoolValue
    {
        get => Value != 0;
        set
        {
            if (!Writable)
                return;

            Hold(value ? 1d : 0d);
            _sink.Set(Name, value);
        }
    }

    public AvatarControlRowViewModel(
        AvatarControlRow row,
        IAvatarParameterSink sink,
        Action<AvatarControlRowViewModel>? pinChanged = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _pinChanged = pinChanged;

        Name = row.Name;
        Leaf = row.Leaf;
        Kind = row.Kind;
        Writable = row.Writable;
        Widget = row.Widget;
        IsBuiltIn = row.IsBuiltIn;
        _value = row.Value;
        _hasValue = row.HasValue;
    }

    public void ObserveExternal(double value, bool hasValue)
    {
        if (IsHeld && DateTime.UtcNow < _heldUntilUtc)
            return;

        if (IsHeld)
            IsHeld = false;

        if (Value != value)
        {
            Value = value;
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(StateWord));
        }

        if (HasValue != hasValue)
            HasValue = hasValue;
    }

    [RelayCommand]
    private void TogglePin()
    {
        if (!CanPin)
            return;

        IsPinned = !IsPinned;
        _pinChanged?.Invoke(this);
    }

    [RelayCommand]
    private void Step(string? direction)
    {
        if (!Writable || Kind != SignalKind.Int)
            return;

        int delta = direction == "down" ? -1 : 1;
        int next = Math.Max(0, (int)Value + delta);

        Hold(next);
        _sink.Set(Name, next);
    }

    [RelayCommand]
    private void CommitFloat()
    {
        if (!Writable || Kind != SignalKind.Float)
            return;

        Hold(Value);
        _sink.Set(Name, (float)Value);
    }

    private void Hold(double value)
    {
        Value = value;
        HasValue = true;
        IsHeld = true;
        _heldUntilUtc = DateTime.UtcNow + HoldWindow;

        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(StateWord));
    }
}
