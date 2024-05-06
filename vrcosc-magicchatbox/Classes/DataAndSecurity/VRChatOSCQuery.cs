//using CommunityToolkit.Mvvm.ComponentModel;
//using System;
//using System.Linq;
//using System.IO;
//using VRC.OSCQuery;
//using Newtonsoft.Json;
//using Microsoft.Extensions.Logging.Abstractions;
//using System.Collections.Generic;
//using System.Net;

//namespace vrcosc_magicchatbox.Classes.DataAndSecurity
//{
//    public partial class VRChatOSCQuerySettings : ObservableObject
//    {

//        private const string SettingsFileName = "VRChatOSCQuerySettings.json";

//        [ObservableProperty]
//        string oSCAddress;

//        [ObservableProperty]
//        int oSCPort;



//        private VRChatOSCQuerySettings()
//        {
//            LoadSettings();
//        }

//        public static VRChatOSCQuerySettings LoadSettings()
//        {
//            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);

//            if (File.Exists(path))
//            {
//                var settingsJson = File.ReadAllText(path);

//                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
//                {
//                    Logging.WriteInfo("The settings JSON file is empty or corrupted.");
//                    return new VRChatOSCQuerySettings();
//                }

//                try
//                {
//                    var settings = JsonConvert.DeserializeObject<VRChatOSCQuerySettings>(settingsJson);

//                    if (settings != null)
//                    {
//                        return settings;
//                    }
//                    else
//                    {
//                        Logging.WriteInfo("Failed to deserialize the settings JSON.");
//                        return new VRChatOSCQuerySettings();
//                    }
//                }
//                catch (JsonException ex)
//                {
//                    Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
//                    return new VRChatOSCQuerySettings();
//                }
//            }
//            else
//            {
//                Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
//                return new VRChatOSCQuerySettings();
//            }
//        }

//        public void SaveSettings()
//        {
//            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
//            Directory.CreateDirectory(Path.GetDirectoryName(path)); // Ensure directory exists
//            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
//            File.WriteAllText(path, settingsJson);
//        }
//    }

//    public partial class VRChatOSCQuery : ObservableObject
//    {
//        using System;
//using System.Collections.Generic;
//using System.Net;
//using VRC.OSCQuery;

//public class VRChatOSC : IDisposable
//    {
//        private OSCQueryService _oscQueryService;
//        private Dictionary<string, OSCQueryNode> _avatarParameters = new Dictionary<string, OSCQueryNode>();

//        public event Action<string, object> OnAvatarParameterChanged;

//        public VRChatOSC()
//        {
//            // Create a new OSCQueryService instance
//            _oscQueryService = new OSCQueryServiceBuilder()
//                .WithDefaults()
//                .WithServiceName("MyOSCService")
//                .WithLogger(new NullLogger<OSCQueryService>())
//                .Build();

//            // Subscribe to OSC and OSCQuery service discovery events
//            _oscQueryService.OnOscServiceAdded += OnOscServiceDiscovered;
//            _oscQueryService.OnOscQueryServiceAdded += OnOscQueryServiceDiscovered;

//            // Subscribe to avatar change events
//            _oscQueryService.RootNode.AddNode(new OSCQueryNode("/avatar/change")). += OnAvatarChanged;
//        }

//        private void OnOscServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSC service: {profile.Name} at {profile.Address}:{profile.Port}");
//        }

//        private void OnOscQueryServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSCQuery service: {profile.Name} at {profile.Address}:{profile.Port}");
//        }

//        private void OnAvatarChanged(object sender, ValueChangedEventArgs args)
//        {
//            string avatarId = (string)args.Value;
//            Console.WriteLine($"Avatar changed to: {avatarId}");

//            // Clear existing avatar parameters
//            _avatarParameters.Clear();

//            // Get the new avatar's OSC tree
//            IPAddress ip = _oscQueryService.HostIP;
//            int port = _oscQueryService.TcpPort;
//            var oscTree = Extensions.GetOSCTree(ip, port).Result;

//            // Find all avatar parameters in the OSC tree
//            var parameterNodes = oscTree.GetNodeWithPath("/avatar/parameters")?.Contents;
//            if (parameterNodes != null)
//            {
//                foreach (var paramNode in parameterNodes.Values)
//                {
//                    _avatarParameters[paramNode.Name] = paramNode;
//                    paramNode.OnValueChangedEvent += OnAvatarParameterValueChanged;
//                }
//            }
//        }

//        private void OnAvatarParameterValueChanged(object sender, ValueChangedEventArgs args)
//        {
//            var node = (OSCQueryNode)sender;
//            string parameterName = node.Name;
//            OnAvatarParameterChanged?.Invoke(parameterName, args.Value);
//        }

//        public void SetAvatarParameter<T>(string parameterName, T value)
//        {
//            if (_avatarParameters.TryGetValue(parameterName, out var node))
//            {
//                node.Value = new object[] { value };
//            }
//            else
//            {
//                Console.WriteLine($"Avatar parameter not found: {parameterName}");
//            }
//        }

//        public T GetAvatarParameter<T>(string parameterName)
//        {
//            if (_avatarParameters.TryGetValue(parameterName, out var node))
//            {
//                return (T)node.Value[0];
//            }
//            else
//            {
//                Console.WriteLine($"Avatar parameter not found: {parameterName}");
//                return default;
//            }
//        }

//        public void Dispose()
//        {
//            _oscQueryService?.Dispose();
//        }
//    }

//}
