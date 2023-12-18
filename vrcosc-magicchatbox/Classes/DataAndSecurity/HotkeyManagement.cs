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
        private string HotkeyConfigFile;
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
            HotkeyConfigFile = Path.Combine(ViewModel.Instance.DataPath, "HotkeyConfig.json");

            if (File.Exists(HotkeyConfigFile))
            {
                LoadHotkeyConfigurations();
            }
            else
            {
                AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
            }
        }

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;
            SetupLocalHotkey(_mainWindow);
            RegisterAllGlobalHotkeys();
        }

        private void AddKeyBinding(string name, Key key, ModifierKeys modifiers, Action action)
        {
            try
            {

            }
            catch (Exception)
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
                try
                {
                    HotkeyManager.Current.AddOrReplace(kvp.Key, kvp.Value.Key, kvp.Value.Modifiers, OnGlobalHotkeyPressed);
                }
                catch (HotkeyAlreadyRegisteredException ex)
                {
                    MessageBox.Show($"The hotkey {kvp.Value.Modifiers} + {kvp.Value.Key} is already registered by another application.", "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, MSGBox: true);
                }
            }
        }


        public void SaveHotkeyConfigurations()
        {
            // Create a temporary dictionary to hold serializable data.
            var serializableHotkeyActions = new Dictionary<string, object>();

            foreach (var entry in _hotkeyActions)
            {
                // Create a serializable anonymous object containing only the serializable properties.
                var hotkeyInfo = new
                {
                    Key = entry.Value.Key,
                    Modifiers = entry.Value.Modifiers
                };
                serializableHotkeyActions.Add(entry.Key, hotkeyInfo);
            }

            try
            {
                // Serialize the temporary dictionary to JSON.
                var json = JsonConvert.SerializeObject(serializableHotkeyActions, Formatting.Indented);
                File.WriteAllText(HotkeyConfigFile, json);
            }
            catch (IOException ex)
            {
                // Handle I/O exceptions (e.g., file access issues).
                Logging.WriteException(ex, MSGBox: true);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle security exceptions.
                Logging.WriteException(ex, MSGBox: true);
            }
            catch (Exception ex)
            {
                // Handle other exceptions.
                Logging.WriteException(ex, MSGBox: true);
            }
        }


        private void LoadHotkeyConfigurations()
        {
            try
            {
                var json = File.ReadAllText(HotkeyConfigFile);
                _hotkeyActions = JsonConvert.DeserializeObject<Dictionary<string, HotkeyInfo>>(json);

                // Make sure the default hotkey is always present.
                if (!_hotkeyActions.ContainsKey("ToggleVoiceGlobal"))
                {
                    AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
                }
                else
                {
                    _hotkeyActions["ToggleVoiceGlobal"].SetAction(ToggleVoice);
                }
            }
            catch (Exception ex)
            {
                // If there's any error, log it and set up the default hotkey.
                Logging.WriteException(ex, MSGBox: true);
                AddDefaultHotkey("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
            }
        }

        private void AddDefaultHotkey(string name, Key key, ModifierKeys modifiers, Action action)
        {
            var hotkeyInfo = new HotkeyInfo(key, modifiers);
            hotkeyInfo.SetAction(action);
            _hotkeyActions[name] = hotkeyInfo;

            // Attempt to register the hotkey immediately.
            RegisterGlobalHotkey(name, hotkeyInfo);
        }

        private void RegisterGlobalHotkey(string name, HotkeyInfo hotkeyInfo)
        {
            try
            {
                HotkeyManager.Current.AddOrReplace(name, hotkeyInfo.Key, hotkeyInfo.Modifiers, OnGlobalHotkeyPressed);
            }
            catch (HotkeyAlreadyRegisteredException ex)
            {
                MessageBox.Show($"The hotkey {hotkeyInfo.Modifiers} + {hotkeyInfo.Key} is already registered by another application.", "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    hotkeyInfo.Action?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: true);
            }
        }

        private static void SetupLocalHotkey(Window window)
        {
            try
            {

            }
            catch (Exception)
            {

                throw;
            }
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
