using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.ViewModels.Avatar;

public partial class AvatarControlGroupViewModel : ObservableObject
{
    private readonly IReadOnlyList<AvatarControlRow> _source;
    private readonly Func<AvatarControlRow, AvatarControlRowViewModel>? _build;
    private bool _materialised;

    [ObservableProperty] private bool _isExpanded;

    public string Name { get; }

    public string DisplayName { get; }

    public ObservableCollection<AvatarControlRowViewModel> Rows { get; } = new();

    public int RowCount => _source.Count;

    public string Header => $"{DisplayName}  ({RowCount})";

    public AvatarControlGroupViewModel(string name, string displayName, IEnumerable<AvatarControlRowViewModel> rows)
    {
        Name = name;
        DisplayName = displayName;
        _materialised = true;
        _isExpanded = true;

        foreach (AvatarControlRowViewModel row in rows)
            Rows.Add(row);

        _source = new AvatarControlRow[Rows.Count];
    }

    public AvatarControlGroupViewModel(
        string name,
        string displayName,
        IReadOnlyList<AvatarControlRow> source,
        Func<AvatarControlRow, AvatarControlRowViewModel> build,
        bool expanded)
    {
        Name = name;
        DisplayName = displayName;
        _source = source ?? Array.Empty<AvatarControlRow>();
        _build = build;

        if (expanded)
            IsExpanded = true;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
            Materialise();
    }

    public void Materialise()
    {
        if (_materialised || _build == null)
            return;

        _materialised = true;

        foreach (AvatarControlRow row in _source)
            Rows.Add(_build(row));
    }
}
