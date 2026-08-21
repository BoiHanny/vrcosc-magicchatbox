using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Updates;

public enum UpdateStepKind
{
    Download,
    Verify,
    Unpack,
    Install
}

public enum UpdateStepStatus
{
    Pending,
    Running,
    Done,
    Warning,
    Failed
}

public partial class UpdateStepViewModel : ObservableObject
{
    public UpdateStepKind Kind { get; }
    public string Label { get; }

    [ObservableProperty] private UpdateStepStatus _status = UpdateStepStatus.Pending;
    [ObservableProperty] private string _detail = string.Empty;

    public UpdateStepViewModel(UpdateStepKind kind, string label)
    {
        Kind = kind;
        Label = label;
    }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public string Glyph => Status switch
    {
        UpdateStepStatus.Done => "✔",
        UpdateStepStatus.Running => "●",
        UpdateStepStatus.Warning => "!",
        UpdateStepStatus.Failed => "✕",
        _ => "○"
    };

    partial void OnStatusChanged(UpdateStepStatus value)
    {
        OnPropertyChanged(nameof(Glyph));
    }

    partial void OnDetailChanged(string value)
    {
        OnPropertyChanged(nameof(HasDetail));
    }
}

public partial class UpdateProgressState : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _headline = string.Empty;
    [ObservableProperty] private string _detail = string.Empty;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private bool _isCompleted;

    public ObservableCollection<UpdateStepViewModel> Steps { get; } =
    [
        new UpdateStepViewModel(UpdateStepKind.Download, "Download"),
        new UpdateStepViewModel(UpdateStepKind.Verify, "Verify integrity"),
        new UpdateStepViewModel(UpdateStepKind.Unpack, "Unpack"),
        new UpdateStepViewModel(UpdateStepKind.Install, "Install")
    ];

    public UpdateStepViewModel Step(UpdateStepKind kind) =>
        Steps.First(step => step.Kind == kind);

    public void Begin(string headline)
    {
        Headline = headline;
        Detail = string.Empty;
        Percent = 0;
        IsIndeterminate = true;
        IsFailed = false;
        IsCompleted = false;

        foreach (UpdateStepViewModel step in Steps)
        {
            step.Status = UpdateStepStatus.Pending;
            step.Detail = string.Empty;
        }

        IsActive = true;
    }

    public void SetStep(UpdateStepKind kind, UpdateStepStatus status, string detail = "")
    {
        UpdateStepViewModel step = Step(kind);
        step.Status = status;
        step.Detail = detail;
    }

    public void Report(double percent, string detail)
    {
        IsIndeterminate = false;
        Percent = Math.Clamp(percent, 0, 100);
        Detail = detail;
    }

    public void ReportIndeterminate(string detail)
    {
        IsIndeterminate = true;
        Detail = detail;
    }

    public void Complete(string detail)
    {
        IsIndeterminate = false;
        Percent = 100;
        Detail = detail;
        IsCompleted = true;
    }

    public void Fail(string detail)
    {
        IsIndeterminate = false;
        Detail = detail;
        IsFailed = true;

        foreach (UpdateStepViewModel step in Steps.Where(s => s.Status == UpdateStepStatus.Running))
        {
            step.Status = UpdateStepStatus.Failed;
        }
    }

    public bool CanDismiss => IsFailed || IsCompleted;

    partial void OnIsFailedChanged(bool value) => OnPropertyChanged(nameof(CanDismiss));

    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(CanDismiss));

    [RelayCommand]
    public void Reset()
    {
        IsActive = false;
        IsFailed = false;
        IsCompleted = false;
        Headline = string.Empty;
        Detail = string.Empty;
        Percent = 0;
    }

    public IReadOnlyList<UpdateStepKind> CompletedSteps() =>
        Steps.Where(s => s.Status is UpdateStepStatus.Done or UpdateStepStatus.Warning)
             .Select(s => s.Kind)
             .ToArray();

    public static string DescribeBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        return bytes >= megabyte
            ? (bytes / megabyte).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
            : Math.Max(1, bytes / 1024d).ToString("0", CultureInfo.InvariantCulture) + " KB";
    }

    public static string DescribeTransfer(long received, long? total, TimeSpan elapsed)
    {
        string rate = string.Empty;
        if (elapsed.TotalSeconds >= 0.5 && received > 0)
        {
            double bytesPerSecond = received / elapsed.TotalSeconds;
            rate = " · " + DescribeBytes((long)bytesPerSecond) + "/s";
        }

        return total is > 0
            ? $"{DescribeBytes(received)} of {DescribeBytes(total.Value)}{rate}"
            : $"{DescribeBytes(received)}{rate}";
    }

    public static double PercentOf(long done, long? total) =>
        total is > 0 ? Math.Clamp(done * 100d / total.Value, 0, 100) : 0;
}
