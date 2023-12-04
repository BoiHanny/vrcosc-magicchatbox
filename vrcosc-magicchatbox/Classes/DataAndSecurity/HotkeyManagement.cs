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
                AddKeyBinding("ToggleVoiceGlobal", Key.V, ModifierKeys.Alt, ToggleVoice);
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
                    MessageBox.Show($"The hotkey {kvp.Value.Modifiers} + {kvp.Value.Key} is already registered by another application. Please choose a different hotkey.", "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, MSGBox: true);
                }
            }
        }


        public void SaveHotkeyConfigurations()
        {
            var json = JsonConvert.SerializeObject(_hotkeyActions, Formatting.Indented);
            File.WriteAllText(HotkeyConfigFile, json);
        }

        private void LoadHotkeyConfigurations()
        {
            try
            {
                var json = File.ReadAllText(HotkeyConfigFile);
                _hotkeyActions = JsonConvert.DeserializeObject<Dictionary<string, HotkeyInfo>>(json);
                _hotkeyActions["ToggleVoiceGlobal"].SetAction(ToggleVoice);
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
