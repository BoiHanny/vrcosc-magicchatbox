using Newtonsoft.Json;
using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class HotkeyManagement
    {
        private static HotkeyManagement _instance;
        private Window _mainWindow;
        private readonly string HotkeyConfigFile;
        private Dictionary<string, HotkeyInfo> _hotkeyActions;

        [JsonObject(MemberSerialization.OptIn)]
        private class HotkeyInfo
        {
            [JsonProperty] public Key Key { get; private set; }
            [JsonProperty] public ModifierKeys Modifiers { get; private set; }
            public Action Action { get; private set; }

            [JsonConstructor]
            public HotkeyInfo(Key key, ModifierKeys modifiers)
            {
                Key = key;
                Modifiers = modifiers;
                Action = null;
            }

            public void SetAction(Action action)
            {
                Action = action;
            }
        }

        public static HotkeyManagement Instance => _instance ?? (_instance = new HotkeyManagement());

        private HotkeyManagement()
        {
            _hotkeyActions = new Dictionary<string, HotkeyInfo>();
            HotkeyConfigFile = Path.Combine(ViewModel.Instance.DataPath, "HotkeyConfigV1.json");

            LoadHotkeyConfigurations(); // Refactored to handle exceptions internally.
        }

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;
            SetupLocalHotkey(_mainWindow);
            RegisterAllGlobalHotkeys();
        }

        private void AddKeyBinding(string name, Key key, ModifierKeys modifiers, Action action)
        {
            if (!_hotkeyActions.ContainsKey(name))
            {
                var hotkeyInfo = new HotkeyInfo(key, modifiers);
                hotkeyInfo.SetAction(action);
                _hotkeyActions[name] = hotkeyInfo;
            }
        }

        private void RegisterAllGlobalHotkeys()
        {
            foreach (var kvp in _hotkeyActions)
            {
                RegisterGlobalHotkey(kvp.Key, kvp.Value);
            }
        }

        public void SaveHotkeyConfigurations()
        {
            var serializableHotkeyActions = new Dictionary<string, object>();
            foreach (var entry in _hotkeyActions)
            {
                var hotkeyInfo = new { Key = entry.Value.Key, Modifiers = entry.Value.Modifiers };
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

        private void LoadHotkeyConfigurations()
        {
            try
            {
                if (!File.Exists(HotkeyConfigFile))
                {
                    AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
                    SaveHotkeyConfigurations();
                    return;
                }

                var json = File.ReadAllText(HotkeyConfigFile);
                _hotkeyActions = JsonConvert.DeserializeObject<Dictionary<string, HotkeyInfo>>(json) ?? new Dictionary<string, HotkeyInfo>();
                if (!_hotkeyActions.ContainsKey("ToggleVoiceGlobal"))
                {
                    AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex: ex, MSGBox: false);
                AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
            }
        }

        private void AddDefaultHotkey(string name, Key key, ModifierKeys modifiers, Action action)
        {
            var hotkeyInfo = new HotkeyInfo(key, modifiers);
            hotkeyInfo.SetAction(action);
            _hotkeyActions[name] = hotkeyInfo;
            RegisterGlobalHotkey(name, hotkeyInfo);
        }

        private void RegisterGlobalHotkey(string name, HotkeyInfo hotkeyInfo)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace(name, hotkeyInfo.Key, hotkeyInfo.Modifiers, OnGlobalHotkeyPressed);
            }
            catch (HotkeyAlreadyRegisteredException)
            {
                Logging.WriteException(new Exception($"Hotkey {name} is already registered"), MSGBox: true);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex: ex, MSGBox: false);
            }
        }

        private void OnGlobalHotkeyPressed(object sender, HotkeyEventArgs e)
        {
            try
            {
                if (_hotkeyActions.TryGetValue(e.Name, out HotkeyInfo hotkeyInfo))
                {
                    hotkeyInfo.Action?.Invoke();
                }
            }
            catch (Exception ex)
            {
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
    }
}
