using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vrcosc_magicchatbox.ViewModels.Avatar;

public partial class AvatarControlGroupViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;

    public string Name { get; }
    public string DisplayName { get; }

    public ObservableCollection<AvatarControlRowViewModel> Rows { get; } = new();

    public int RowCount => Rows.Count;

    public AvatarControlGroupViewModel(string name, string displayName, IEnumerable<AvatarControlRowViewModel> rows)
    {
        Name = name;
        DisplayName = displayName;

        foreach (AvatarControlRowViewModel row in rows)
            Rows.Add(row);
    }
}
