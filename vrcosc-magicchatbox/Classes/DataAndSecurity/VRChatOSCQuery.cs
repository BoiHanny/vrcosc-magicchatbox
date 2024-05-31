//using CommunityToolkit.Mvvm.ComponentModel;
//using System;
//using System.Linq;
//using System.IO;
//using VRC.OSCQuery;
//using Newtonsoft.Json;
//using Microsoft.Extensions.Logging.Abstractions;
//using System.Collections.Generic;
//using System.Net;
//using System.Threading.Tasks;

//namespace vrcosc_magicchatbox.Classes.DataAndSecurity
//{
//    public partial class VRChatOSCQuerySettings : ObservableObject
//    {
//        private const string SettingsFileName = "VRChatOSCQuerySettings.json";
//        private static readonly Lazy<VRChatOSCQuerySettings> _instance = new Lazy<VRChatOSCQuerySettings>(LoadSettings);

//        public static VRChatOSCQuerySettings Instance => _instance.Value;

//        [ObservableProperty]
//        private string oSCAddress;

//        [ObservableProperty]
//        private int oSCPort;

//        private VRChatOSCQuerySettings() { }

//        private static VRChatOSCQuerySettings LoadSettings()
//        {
//            var path = GetSettingsFilePath();

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
//                    return settings ?? new VRChatOSCQuerySettings();
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
//            var path = GetSettingsFilePath();
//            Directory.CreateDirectory(Path.GetDirectoryName(path));
//            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
//            File.WriteAllText(path, settingsJson);
//        }

//        private static string GetSettingsFilePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
//    }


//    public partial class VRChatOSCQuery : ObservableObject, IDisposable
//    {
//        private readonly OSCQueryService _oscQueryService;
//        private readonly Dictionary<string, OSCQueryNode> _avatarParameters = new();

//        public event Action<string, object> OnAvatarParameterChanged;

//        public VRChatOSCQuery()
//        {
//            _oscQueryService = new OSCQueryServiceBuilder()
//                .WithDefaults()
//                .WithServiceName("MCb-client")
//                .Build();

//            _oscQueryService.OnOscServiceAdded += OnOscServiceDiscovered;
//            _oscQueryService.OnOscQueryServiceAdded += OnOscQueryServiceDiscovered;
//            _oscQueryService.RootNode.AddNode(new OSCQueryNode("/avatar/change"));
//        }

//        private void OnOscServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSC service: {profile.name} at {profile.address}:{profile.port}");
//        }

//        private void OnOscQueryServiceDiscovered(OSCQueryServiceProfile profile)
//        {
//            Console.WriteLine($"Discovered OSCQuery service: {profile.name} at {profile.address}:{profile.port}");
//        }

//        public async Task RefreshAvatarParameters()
//        {
//            _avatarParameters.Clear();

//            try
//            {
//                var ip = _oscQueryService.HostIP;
//                var port = _oscQueryService.TcpPort;
//                var oscTree = await Extensions.GetOSCTree(ip, port);

//                var parameterNodes = oscTree.GetNodeWithPath("/avatar/parameters")?.Contents;
//                if (parameterNodes != null)
//                {
//                    foreach (var paramNode in parameterNodes.Values)
//                    {
//                        _avatarParameters[paramNode.Name] = paramNode;
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error refreshing avatar parameters: {ex.Message}");
//            }
//        }

//        public void SetAvatarParameter<T>(string parameterName, T value)
//        {
//            if (_avatarParameters.TryGetValue(parameterName, out var node))
//            {
//                node.Value = new object[] { value };
//                OnAvatarParameterChanged?.Invoke(parameterName, value);
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
