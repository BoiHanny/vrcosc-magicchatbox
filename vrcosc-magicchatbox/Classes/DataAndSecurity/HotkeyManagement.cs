﻿using Newtonsoft.Json;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class HotkeyManagement
    {
        private static HotkeyManagement _instance;
        private Dictionary<string, HotkeyInfo> _hotkeyActions;
        private Window _mainWindow;
        private readonly string HotkeyConfigFile;

        private HotkeyManagement()
        {
            _hotkeyActions = new Dictionary<string, HotkeyInfo>();
            HotkeyConfigFile = Path.Combine(ViewModel.Instance.DataPath, "HotkeyConfiguration.json");
            LoadHotkeyConfigurations();
        }

        private void AddDefaultHotkeys()
        {
            AddKeyBinding("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
            // Add other default hotkeys here
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
                // Add other hotkey actions here
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

                // Check if the JSON string is empty, contains only null characters, or is whitespace
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
                    Application.Current.Dispatcher.Invoke(hotkeyInfo.Action);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, false);
            }
        }

        private void RegisterAllGlobalHotkeys()
        {
            foreach (var kvp in _hotkeyActions)
            {
                RegisterGlobalHotkey(kvp.Key, kvp.Value);
            }
        }


        private void RegisterGlobalHotkey(string name, HotkeyInfo hotkeyInfo)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace(name, hotkeyInfo.Key, hotkeyInfo.Modifiers, OnGlobalHotkeyPressed);
            }
            catch (HotkeyAlreadyRegisteredException)
            {
                // Handle already registered hotkey case
                Logging.WriteException(new Exception($"Hotkey {name} is already registered"), MSGBox: true, autoclose: true);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Logging.WriteException(ex: ex, MSGBox: false);
            }
        }


        private static void SetupLocalHotkey(Window window)
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

        private static void ToggleVoice()
        {
            ViewModel.Instance.ToggleVoiceCommand.Execute(null);
        }

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;
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

        public static HotkeyManagement Instance => _instance ?? (_instance = new HotkeyManagement());

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
}
