namespace vrcosc_magicchatbox.Core.Services;

public interface IStatusListService
{
    void LoadStatusList();
    void SaveStatusList();

    void RequestSave();

    void AddGroup(string name);

    void RenameGroup(string groupId, string newName);

    void DeleteGroup(string groupId);

    string ExportGroupToJson(string groupId);

    string ExportItemsToJson(System.Collections.Generic.IEnumerable<vrcosc_magicchatbox.ViewModels.StatusItem> items);

    int ImportFromJson(string json);
}
