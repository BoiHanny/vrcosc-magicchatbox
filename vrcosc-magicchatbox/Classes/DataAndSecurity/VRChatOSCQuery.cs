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
//        public partial class VRChatOSCQuerySettings : ObservableObject
//        {
//            private const string SettingsFileName = "VRChatOSCQuerySettings.json";

//            [ObservableProperty]
//            private string oSCAddress;

//            [ObservableProperty]
//            private int oSCPort;

//            private VRChatOSCQuerySettings()
//            {
//                // Load settings from file during initialization
//                LoadSettings();
//            }

//            // Singleton pattern to ensure a single instance of settings
//            public static VRChatOSCQuerySettings Instance { get; } = LoadSettings();

//            private static VRChatOSCQuerySettings LoadSettings()
//            {
//                var path = GetSettingsFilePath();

//                if (File.Exists(path))
//                {
//                    var settingsJson = File.ReadAllText(path);

//                    if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
//                    {
//                        Logging.WriteInfo("The settings JSON file is empty or corrupted.");
//                        return new VRChatOSCQuerySettings();
//                    }

//                    try
//                    {
//                        var settings = JsonConvert.DeserializeObject<VRChatOSCQuerySettings>(settingsJson);
//                        return settings ?? new VRChatOSCQuerySettings();
//                    }
//                    catch (JsonException ex)
//                    {
//                        Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
//                        return new VRChatOSCQuerySettings();
//                    }
//                }
//                else
//                {
//                    Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
//                    return new VRChatOSCQuerySettings();
//                }
//            }

//            public void SaveSettings()
//            {
//                var path = GetSettingsFilePath();
//                Directory.CreateDirectory(Path.GetDirectoryName(path));
//                var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
//                File.WriteAllText(path, settingsJson);
//            }

//            private static string GetSettingsFilePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
//        }


//public partial class VRChatOSCQuery : ObservableObject, IDisposable
//    {
//        private readonly OSCQueryService _oscQueryService;
//        private readonly Dictionary<string, OSCQueryNode> _avatarParameters = new();

//        public event Action<string, object> OnAvatarParameterChanged;

//        public VRChatOSCQuery()
//        {
//            _oscQueryService = new OSCQueryServiceBuilder()
//                .WithDefaults()
//                .WithServiceName("MyOSCService")
//                .WithLogger(new NullLogger<OSCQueryService>())
//                .Build();

//            _oscQueryService.OnOscServiceAdded += OnOscServiceDiscovered;
//            _oscQueryService.OnOscQueryServiceAdded += OnOscQueryServiceDiscovered;

//            var rootNode = _oscQueryService.RootNode;
//            var avatarChangeNode = new OSCQueryNode("/avatar/change");
//            avatarChangeNode.OnValueChangedEvent += OnAvatarChanged;
//            rootNode.AddNode(avatarChangeNode);
//        }

//        private void OnOscServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSC service: {profile.name} at {profile.address}:{profile.port}");
//        }

//        private void OnOscQueryServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSCQuery service: {profile.name} at {profile.address}:{profile.port}");
//        }

//        private async void OnAvatarChanged(object sender, ValueChangedEventArgs args)
//        {
//            string avatarId = (string)args.Value;
//            Console.WriteLine($"Avatar changed to: {avatarId}");

//            _avatarParameters.Clear();

//            IPAddress ip = _oscQueryService.HostIP;
//            int port = _oscQueryService.TcpPort;
//            var oscTree = await Extensions.GetOSCTree(ip, port);

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
