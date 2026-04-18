using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Loads, saves, and manages status items and groups for the chatbox status display.
/// JSON format (v2): { "version": 2, "groups": [...], "items": [...] }
/// Legacy format (v1): raw array [...] — auto-migrated to Default group on first load.
/// </summary>
public sealed class StatusListService : IStatusListService
{
    private const int CurrentSchemaVersion = 2;
    private const string DefaultGroupName = "Default";

    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IAppState _appState;
    private readonly IEnvironmentService _env;
    private readonly IUiDispatcher _dispatcher;
    private readonly IToastService _toast;

    public StatusListService(ChatStatusDisplayState chatStatus, IAppState appState, IEnvironmentService env, IUiDispatcher dispatcher, IToastService toast)
    {
        _chatStatus = chatStatus;
        _appState = appState;
        _env = env;
        _dispatcher = dispatcher;
        _toast = toast;
    }

    public void LoadStatusList()
    {
        try
        {
            string statusListPath = Path.Combine(_env.DataPath, "StatusList.json");
            string statusListLegacy = Path.Combine(_env.DataPath, "StatusList.xml");
            string? statusListFile = File.Exists(statusListPath) ? statusListPath
                                   : File.Exists(statusListLegacy) ? statusListLegacy : null;
            if (statusListFile != null)
            {
                string json = File.ReadAllText(statusListFile);
                LoadFromJson(json);
            }
            else
            {
                InitializeWithDefaults();
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("📋 Status List", "Failed to load status list — using defaults.", ToastType.Warning, key: "status-list-load-failed");
            EnsureInitialized();
        }
    }

    public void SaveStatusList()
    {
        try
        {
            var dataPath = _env.DataPath;
            if (string.IsNullOrEmpty(dataPath)) return;

            Directory.CreateDirectory(dataPath);

            var bundle = new StatusBundle
            {
                Version = CurrentSchemaVersion,
                Groups = _chatStatus.GroupList.ToList(),
                Items = _chatStatus.StatusList.ToList()
            };
            var json = JsonConvert.SerializeObject(bundle, Formatting.Indented);
            File.WriteAllText(Path.Combine(dataPath, "StatusList.json"), json);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("💾 Status List", "Failed to save status list changes.", ToastType.Warning, key: "status-list-save-failed");
        }
    }

    public void AddGroup(string name)
    {
        var group = new StatusGroup
        {
            GroupId = Guid.NewGuid().ToString(),
            Name = name.Trim(),
            IsActiveForCycle = true,
            CreationDate = DateTime.Now
        };
        _dispatcher.Invoke(() => _chatStatus.GroupList.Add(group));
        SaveStatusList();
    }

    public void RenameGroup(string groupId, string newName)
    {
        var group = _chatStatus.GroupList.FirstOrDefault(g => g.GroupId == groupId);
        if (group == null) return;
        group.Name = newName.Trim();
        SaveStatusList();
    }

    public void DeleteGroup(string groupId)
    {
        var group = _chatStatus.GroupList.FirstOrDefault(g => g.GroupId == groupId);
        if (group == null) return;

        // Never delete Default group
        var defaultGroup = _chatStatus.GroupList.FirstOrDefault(g => g.Name == DefaultGroupName);
        if (defaultGroup == null || group.GroupId == defaultGroup.GroupId) return;

        // Move orphaned items to Default
        foreach (var item in _chatStatus.StatusList.Where(i => i.GroupId == groupId))
            item.GroupId = defaultGroup.GroupId;

        _dispatcher.Invoke(() => _chatStatus.GroupList.Remove(group));
        SaveStatusList();
    }

    // ── private ──────────────────────────────────────────────────────────────

    private void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            SetState(new ObservableCollection<StatusGroup>(), new ObservableCollection<StatusItem>(), checkEggs: false);
            return;
        }

        try
        {
            var root = JToken.Parse(json);

            if (root is JArray)
            {
                // v1 legacy: raw items array — migrate to Default group
                MigrateFromLegacyArray(root.ToObject<ObservableCollection<StatusItem>>()
                                       ?? new ObservableCollection<StatusItem>());
                return;
            }

            var bundle = root.ToObject<StatusBundle>();
            if (bundle == null)
            {
                InitializeWithDefaults();
                return;
            }

            var groups = new ObservableCollection<StatusGroup>(bundle.Groups ?? Enumerable.Empty<StatusGroup>());
            var items = new ObservableCollection<StatusItem>(bundle.Items ?? Enumerable.Empty<StatusItem>());

            EnsureDefaultGroup(groups);
            AssignOrphanedItemsToDefault(items, groups);

            SetState(groups, items, checkEggs: true);
        }
        catch (JsonException ex)
        {
            Logging.WriteException(ex, MSGBox: true);
            EnsureInitialized();
        }
    }

    private void MigrateFromLegacyArray(ObservableCollection<StatusItem> items)
    {
        var defaultGroup = new StatusGroup
        {
            GroupId = Guid.NewGuid().ToString(),
            Name = DefaultGroupName,
            IsActiveForCycle = true,
            CreationDate = DateTime.Now
        };
        var groups = new ObservableCollection<StatusGroup> { defaultGroup };

        foreach (var item in items)
            item.GroupId = defaultGroup.GroupId;

        SetState(groups, items, checkEggs: true);
        SaveStatusList(); // persist v2 format immediately
        Logging.WriteInfo("StatusList migrated from legacy array format to v2 bundle format.");
    }

    private static void EnsureDefaultGroup(ObservableCollection<StatusGroup> groups)
    {
        if (!groups.Any(g => g.Name == DefaultGroupName))
        {
            groups.Insert(0, new StatusGroup
            {
                GroupId = Guid.NewGuid().ToString(),
                Name = DefaultGroupName,
                IsActiveForCycle = true,
                CreationDate = DateTime.MinValue
            });
        }
    }

    private static void AssignOrphanedItemsToDefault(ObservableCollection<StatusItem> items, ObservableCollection<StatusGroup> groups)
    {
        var defaultGroup = groups.First(g => g.Name == DefaultGroupName);
        var validIds = groups.Select(g => g.GroupId).ToHashSet();
        foreach (var item in items.Where(i => i.GroupId == null || !validIds.Contains(i.GroupId)))
            item.GroupId = defaultGroup.GroupId;
    }

    private void SetState(ObservableCollection<StatusGroup> groups, ObservableCollection<StatusItem> items, bool checkEggs)
    {
        void Apply()
        {
            _chatStatus.GroupList = groups;
            _chatStatus.StatusList = items;
            if (checkEggs) CheckForSpecialMessages(items);
        }

        if (!_dispatcher.CheckAccess()) _dispatcher.Invoke(Apply);
        else Apply();
    }

    private void CheckForSpecialMessages(ObservableCollection<StatusItem> statusList)
    {
        if (statusList.Any(x => x.msg.Equals("boihanny", StringComparison.OrdinalIgnoreCase) ||
                                x.msg.Equals("sr4 series", StringComparison.OrdinalIgnoreCase)))
            _appState.Egg_Dev = true;

        if (statusList.Any(x => x.msg.Equals("bussyboys", StringComparison.OrdinalIgnoreCase)))
            _appState.BussyBoysMode = true;
    }

    private void InitializeWithDefaults()
    {
        var defaultGroup = new StatusGroup
        {
            GroupId = Guid.NewGuid().ToString(),
            Name = DefaultGroupName,
            IsActiveForCycle = true,
            CreationDate = DateTime.Now
        };
        var groups = new ObservableCollection<StatusGroup> { defaultGroup };

        var items = new ObservableCollection<StatusItem>
        {
            new StatusItem { CreationDate = DateTime.Now, IsActive = true, msg = "Enjoy 💖", MSGID = GenerateRandomId(), GroupId = defaultGroup.GroupId },
            new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Below you can create your own status", MSGID = GenerateRandomId(), GroupId = defaultGroup.GroupId },
            new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Activate it by clicking the power icon", MSGID = GenerateRandomId(), GroupId = defaultGroup.GroupId }
        };

        SetState(groups, items, checkEggs: false);
        SaveStatusList();
    }

    private void EnsureInitialized()
    {
        if (_chatStatus.StatusList.Count == 0 && _chatStatus.GroupList.Count == 0)
            InitializeWithDefaults();
    }

    private static int GenerateRandomId()
    {
        var random = new Random();
        return random.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax);
    }

    // ── inner DTO ─────────────────────────────────────────────────────────────

    private sealed class StatusBundle
    {
        public int Version { get; set; } = CurrentSchemaVersion;
        public System.Collections.Generic.List<StatusGroup>? Groups { get; set; }
        public System.Collections.Generic.List<StatusItem>? Items { get; set; }
    }
}

