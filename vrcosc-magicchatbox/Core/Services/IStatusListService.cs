namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages status list and group persistence and CRUD (StatusList.json).
/// Load populates ChatStatusDisplayState.StatusList and GroupList.
/// Save serializes the current shared state.
/// </summary>
public interface IStatusListService
{
    void LoadStatusList();
    void SaveStatusList();

    /// <summary>Schedule a debounced save (2 seconds). Resets if called again within the window.</summary>
    void RequestSave();

    /// <summary>Add a new group with the given name. Saves immediately.</summary>
    void AddGroup(string name);

    /// <summary>Rename the group with the given ID. Saves immediately.</summary>
    void RenameGroup(string groupId, string newName);

    /// <summary>Delete a group. Items in the group are moved to the Default group. Saves immediately.</summary>
    void DeleteGroup(string groupId);

    /// <summary>Export a group and its items as a shareable JSON string.</summary>
    string ExportGroupToJson(string groupId);

    /// <summary>Export the specified items as a shareable JSON string.</summary>
    string ExportItemsToJson(System.Collections.Generic.IEnumerable<vrcosc_magicchatbox.ViewModels.StatusItem> items);

    /// <summary>Import groups and items from a JSON string. Returns the number of items imported.</summary>
    int ImportFromJson(string json);
}
