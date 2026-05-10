using Newtonsoft.Json;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Manages global and local hotkey registration, persistence, and dispatch.
/// Configuration is stored as JSON in the user's data directory.
/// </summary>
public class HotkeyManagement
{
    private readonly TtsSettings _ttsSettings;
    private readonly AppSettings _appSettings;
    private readonly IOscSender _oscSender;
    private readonly IUiDispatcher _dispatcher;
    private readonly Lazy<ITrayIconService> _trayIconService;

    private Dictionary<string, HotkeyInfo> _hotkeyActions;
    private Window _mainWindow;
    private readonly string HotkeyConfigFile;
    private bool _isInitialized;

    public string TrayShortcutDisplayText { get; private set; } = string.Empty;

    public HotkeyManagement(
        IEnvironmentService env,
        IOscSender oscSender,
        ISettingsProvider<TtsSettings> ttsSettings,
        ISettingsProvider<AppSettings> appSettings,
        IUiDispatcher dispatcher,
        Lazy<ITrayIconService> trayIconService)
    {
        _oscSender = oscSender;
        _ttsSettings = ttsSettings.Value;
        _appSettings = appSettings.Value;
        _dispatcher = dispatcher;
        _trayIconService = trayIconService;
        _hotkeyActions = new Dictionary<string, HotkeyInfo>();
        HotkeyConfigFile = Path.Combine(env.DataPath, "HotkeyConfiguration.json");
        LoadHotkeyConfigurations();
        _appSettings.PropertyChanged += AppSettings_PropertyChanged;
    }

    private void AddDefaultHotkeys()
    {
        AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
        AddKeyBinding("OpenTrayMenuGlobal", Key.X, ModifierKeys.Alt, OpenTrayMenu);
    }

    private void AddDefaultHotkey(string name, Key key, ModifierKeys modifiers, Action action)
    {
        if (!_hotkeyActions.ContainsKey(name))
            AddKeyBinding(name, key, modifiers, action);
    }

    private void AddKeyBinding(string name, Key key, ModifierKeys modifiers, Action action)
    {
        _hotkeyActions[name] = new HotkeyInfo(key, modifiers, action);
    }



    private Action GetActionForHotkey(string hotkeyName)
    {
        return hotkeyName switch
        {
            "ToggleVoiceGlobal" => ToggleVoice,
            "OpenTrayMenuGlobal" => OpenTrayMenu,
            _ => null
        };
    }

    private void LoadHotkeyConfigurations()
    {
        try
        {
            if (!File.Exists(HotkeyConfigFile))
            {
                AddDefaultHotkeys();
                SaveHotkeyConfigurations();
                return;
            }

            var json = File.ReadAllText(HotkeyConfigFile);

            if (string.IsNullOrWhiteSpace(json) || json.All(c => c == '\0'))
            {
                Logging.WriteException(new Exception("The hotkey configurations file is empty or corrupted."), MSGBox: false);
                AddDefaultHotkeys();
                SaveHotkeyConfigurations();
                return;
            }

            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            if (deserialized == null)
            {
                Logging.WriteException(new Exception("Failed to deserialize hotkey configurations."), MSGBox: true);
                AddDefaultHotkeys();
                return;
            }

            _hotkeyActions.Clear();
            foreach (var entry in deserialized)
            {
                if (!Enum.TryParse<Key>(entry.Value["Key"], out var key) ||
                    !Enum.TryParse<ModifierKeys>(entry.Value["Modifiers"], out var modifiers))
                {
                    Logging.WriteException(new Exception($"Failed to parse hotkey configuration for {entry.Key}."), MSGBox: true);
                    continue;
                }

                var action = GetActionForHotkey(entry.Key);
                if (action == null)
                {
                    Logging.WriteException(new Exception($"No action defined for hotkey {entry.Key}."), MSGBox: true);
                    continue;
                }

                AddKeyBinding(entry.Key, key, modifiers, action);
            }

            AddDefaultHotkeys();
            SaveHotkeyConfigurations();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true);
        }
    }

    private void OnGlobalHotkeyPressed(object sender, HotkeyEventArgs e)
    {
        try
        {
            if (_hotkeyActions.TryGetValue(e.Name, out HotkeyInfo hotkeyInfo))
            {
                _dispatcher.BeginInvoke(hotkeyInfo.Action);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, false);
        }
    }

    private void RegisterAllGlobalHotkeys()
    {
        TrayShortcutDisplayText = string.Empty;
        foreach (var kvp in _hotkeyActions)
        {
            if (kvp.Key == "OpenTrayMenuGlobal" && !_appSettings.OpenTrayWithAltX)
                continue;

            RegisterGlobalHotkey(kvp.Key, kvp.Value);
        }
    }

    private void AppSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || e.PropertyName != nameof(AppSettings.OpenTrayWithAltX))
            return;

        _dispatcher.BeginInvoke(UpdateTrayHotkeyRegistration);
    }

    private void UpdateTrayHotkeyRegistration()
    {
        if (!_hotkeyActions.TryGetValue("OpenTrayMenuGlobal", out HotkeyInfo hotkeyInfo))
            return;

        if (_appSettings.OpenTrayWithAltX)
        {
            RegisterGlobalHotkey("OpenTrayMenuGlobal", hotkeyInfo);
            return;
        }

        TrayShortcutDisplayText = string.Empty;
        UnregisterGlobalHotkey("OpenTrayMenuGlobal");
    }

    private static void UnregisterGlobalHotkey(string name)
    {
        try
        {
            HotkeyManager.Current.Remove(name);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Unable to unregister hotkey {name}: {ex.Message}");
        }
    }


    private void RegisterGlobalHotkey(string name, HotkeyInfo hotkeyInfo)
    {
        if (name == "OpenTrayMenuGlobal")
        {
            RegisterTrayHotkey(name, hotkeyInfo);
            return;
        }

        TryRegisterGlobalHotkey(name, hotkeyInfo, showAlreadyRegisteredMessage: true);
    }

    private bool RegisterTrayHotkey(string name, HotkeyInfo hotkeyInfo)
    {
        if (TryRegisterGlobalHotkey(name, hotkeyInfo, showAlreadyRegisteredMessage: false))
        {
            TrayShortcutDisplayText = FormatHotkey(hotkeyInfo);
            return true;
        }

        Logging.WriteInfo("Alt+X tray menu hotkey could not be registered.");
        return false;
    }

    private bool TryRegisterGlobalHotkey(string name, HotkeyInfo hotkeyInfo, bool showAlreadyRegisteredMessage)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace(name, hotkeyInfo.Key, hotkeyInfo.Modifiers, OnGlobalHotkeyPressed);
            return true;
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            if (showAlreadyRegisteredMessage)
                Logging.WriteException(new Exception($"Hotkey {name} is already registered"), MSGBox: true, autoclose: true);
            else
                Logging.WriteInfo($"Hotkey {FormatHotkey(hotkeyInfo)} is already registered.");
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex: ex, MSGBox: false);
        }

        return false;
    }


    private void SetupLocalHotkey(Window window)
    {
        window.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    ToggleVoice();
                    e.Handled = true;
                }
            }
        };
    }

    private void ToggleVoice()
    {
        if (_ttsSettings.ToggleVoiceWithV)
            _oscSender.ToggleVoice(true);
    }

    private void OpenTrayMenu()
    {
        if (_appSettings.OpenTrayWithAltX)
            _trayIconService.Value.OpenContextMenu();
    }

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _isInitialized = true;
        SetupLocalHotkey(_mainWindow);
        RegisterAllGlobalHotkeys();
    }

    public void SaveHotkeyConfigurations()
    {
        var serializableHotkeyActions = new Dictionary<string, object>();
        foreach (var entry in _hotkeyActions)
        {
            var hotkeyInfo = new { Key = entry.Value.Key.ToString(), Modifiers = entry.Value.Modifiers.ToString() };
            serializableHotkeyActions.Add(entry.Key, hotkeyInfo);
        }

        try
        {
            var json = JsonConvert.SerializeObject(serializableHotkeyActions, Formatting.Indented);
            File.WriteAllText(HotkeyConfigFile, json);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex: ex, MSGBox: false);
        }
    }

    private static string FormatHotkey(HotkeyInfo hotkeyInfo)
        => hotkeyInfo.Modifiers == ModifierKeys.None
            ? hotkeyInfo.Key.ToString()
            : $"{hotkeyInfo.Modifiers}+{hotkeyInfo.Key}";

    [JsonObject(MemberSerialization.OptIn)]
    private class HotkeyInfo
    {

        public HotkeyInfo(Key key, ModifierKeys modifiers, Action action = null)
        {
            Key = key;
            Modifiers = modifiers;
            Action = action;
        }

        [JsonIgnore] public Action Action { get; private set; }
        [JsonProperty] public Key Key { get; private set; }
        [JsonProperty] public ModifierKeys Modifiers { get; private set; }
    }
}
