using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace vrcosc_magicchatbox.ViewModels.Models;

/// <summary>
/// A named group that organizes status items and controls auto-cycle eligibility.
/// </summary>
public class StatusGroup : INotifyPropertyChanged
{
    private string _groupId = Guid.NewGuid().ToString();
    private string _name = "Default";
    private bool _isActiveForCycle = true;
    private DateTime _creationDate = DateTime.Now;
    private bool _isRenaming;
    private string _renameBuffer = string.Empty;

    public string GroupId
    {
        get => _groupId;
        set { if (_groupId != value) { _groupId = value; Notify(); } }
    }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; Notify(); } }
    }

    public bool IsActiveForCycle
    {
        get => _isActiveForCycle;
        set { if (_isActiveForCycle != value) { _isActiveForCycle = value; Notify(); } }
    }

    public DateTime CreationDate
    {
        get => _creationDate;
        set { _creationDate = value; Notify(); }
    }

    /// <summary>Transient — not persisted. True while the group name is being edited inline.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsRenaming
    {
        get => _isRenaming;
        set { _isRenaming = value; Notify(); }
    }

    /// <summary>Transient buffer used during inline rename.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string RenameBuffer
    {
        get => _renameBuffer;
        set { _renameBuffer = value; Notify(); }
    }

    /// <summary>Computed — true when this is the protected Default group (cannot be renamed or deleted).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsDefault => Name == "Default";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string name = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
