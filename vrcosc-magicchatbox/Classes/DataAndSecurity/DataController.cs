using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using Version = vrcosc_magicchatbox.ViewModels.Version;

namespace vrcosc_magicchatbox.DataAndSecurity
{
    public static class DataController
    {
        private static bool isUpdateCheckRunning = false;

        private static readonly Dictionary<char, string> SuperscriptMapping = new Dictionary<char, string>
{
    {'a', "ᵃ"}, {'b', "ᵇ"}, {'c', "ᶜ"}, {'d', "ᵈ"}, {'e', "ᵉ"},
    {'f', "ᶠ"}, {'g', "ᵍ"}, {'h', "ʰ"}, {'i', "ⁱ"}, {'j', "ʲ"},
    {'k', "ᵏ"}, {'l', "ˡ"}, {'m', "ᵐ"}, {'n', "ⁿ"}, {'o', "ᵒ"},
    {'p', "ᵖ"}, {'q', "ᵒ"}, {'r', "ʳ"}, {'s', "ˢ"}, {'t', "ᵗ"},
    {'u', "ᵘ"}, {'v', "ᵛ"}, {'w', "ʷ"}, {'x', "ˣ"}, {'y', "ʸ"},
    {'z', "ᶻ"}, {'0', "⁰"}, {'1', "¹"}, {'2', "²"}, {'3', "³"},
    {'4', "⁴"}, {'5', "⁵"}, {'6', "⁶"}, {'7', "⁷"}, {'8', "⁸"},
    {'9', "⁹"}
};

        public static string TransformToSuperscript(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return new string(input.ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .Select(c => char.IsWhiteSpace(c) ? " " : (SuperscriptMapping.ContainsKey(c) ? SuperscriptMapping[c] : c.ToString()))
                .SelectMany(s => s)
                .ToArray());
        }




        private static void CheckForUpdate()
        {
            try
            {
                string token = EncryptionMethods.DecryptString(ViewModel.Instance.ApiStream);
                string urlLatest = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases/latest";
                string urlPreRelease = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases";
                if (urlLatest != null)
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                        client.DefaultRequestHeaders.Add("User-Agent", "vrcosc-magicchatbox-update-checker");

                        // Check the latest release
                        var responseLatest = client.GetAsync(urlLatest).Result;
                        var jsonLatest = responseLatest.Content.ReadAsStringAsync().Result;
                        dynamic releaseLatest = JsonConvert.DeserializeObject(jsonLatest);
                        string latestVersion = releaseLatest.tag_name;

                        ViewModel.Instance.LatestReleaseVersion = new Version(
                            Regex.Replace(latestVersion, "[^0-9.]", string.Empty));
                        ViewModel.Instance.LatestReleaseURL = releaseLatest.assets[0].browser_download_url; // Store the download URL

                        // Check the latest pre-release
                        var responsePreRelease = client.GetAsync(urlPreRelease).Result;
                        var jsonPreRelease = responsePreRelease.Content.ReadAsStringAsync().Result;
                        JArray releases = JArray.Parse(jsonPreRelease);
                        string preReleaseVersion = string.Empty;
                        foreach (var release in releases)
                        {
                            if ((bool)release["prerelease"])
                            {
                                preReleaseVersion = release["tag_name"].ToString();
                                break;
                            }
                        }

                        // Check if there's a new pre-release and user is joined to alpha channel
                        if (ViewModel.Instance.JoinedAlphaChannel && !string.IsNullOrEmpty(preReleaseVersion))
                        {
                            ViewModel.Instance.PreReleaseVersion = new Version(
                                Regex.Replace(preReleaseVersion, "[^0-9.]", string.Empty));
                            ViewModel.Instance.PreReleaseURL = releases[0]["assets"][0]["browser_download_url"].ToString(); // Store the download URL
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                ViewModel.Instance.VersionTxt = "Can't check updates";
                ViewModel.Instance.VersionTxtColor = "#F36734";
                ViewModel.Instance.VersionTxtUnderLine = false;
            }
            finally
            {
                CompareVersions();
            }
        }

        private static object ConvertToType(Type targetType, string value)
        {
            switch (targetType)
            {
                case Type t when t == typeof(bool):
                    return bool.Parse(value);
                case Type t when t == typeof(int):
                    return int.Parse(value);
                case Type t when t == typeof(string):
                    return value;
                case Type t when t == typeof(float):
                    return float.Parse(value);
                case Type t when t == typeof(double):
                    return double.Parse(value);
                case Type t when t == typeof(Timezone):
                    return Enum.Parse(typeof(Timezone), value);
                case Type t when t == typeof(DateTime):
                    return DateTime.Parse(value);
                default:
                    throw new NotSupportedException($"Unsupported type: {targetType}");
            }
        }


        private static XmlNode GetOrCreateNode(XmlDocument xmlDoc, XmlNode rootNode, string nodeName)
        {
            XmlNode node = rootNode.SelectSingleNode(nodeName);
            if (node == null)
            {
                node = xmlDoc.CreateElement(nodeName);
                rootNode.AppendChild(node);
            }
            return node;
        }

        private const string FileName = "ComponentStats.json";

        public static void SaveComponentStats()
        {
            var statsList = ViewModel.Instance.ComponentStatsList;
            var jsonData = JsonConvert.SerializeObject(statsList);
            File.WriteAllText(FileName, jsonData);
        }

        public static void LoadComponentStats()
        {
            if (File.Exists(FileName))
            {
                var jsonData = File.ReadAllText(FileName);
                ObservableCollection<ComponentStatsItem> statsList =
                    JsonConvert.DeserializeObject<ObservableCollection<ComponentStatsItem>>(jsonData) ??
                    new ObservableCollection<ComponentStatsItem>();
                ViewModel.Instance.UpdateComponentStatsList(statsList);
            }
        }

        private static Dictionary<string, (Type type, string category)> InitializeSettingsDictionary()
        {
            return new Dictionary<string, (Type type, string category)>
            {
                { "IntgrStatus", (typeof(bool), "Integrations") },
                { "IntgrScanWindowActivity", (typeof(bool), "Integrations") },
                { "IntgrScanSpotify_OLD", (typeof(bool), "Integrations") },
                { "IntgrScanWindowTime", (typeof(bool), "Integrations") },
                { "ApplicationHookV2", (typeof(bool), "Integrations") },
                { "IntgrHeartRate", (typeof(bool), "Integrations") },
                { "IntgrScanMediaLink", (typeof(bool), "Integrations") },
                { "IntgrComponentStats", (typeof(bool), "Integrations") },


                { "IntgrComponentStats_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrComponentStats_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrStatus_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrStatus_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrMediaLink_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrMediaLink_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrWindowActivity_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrWindowActivity_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrHeartRate_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrHeartRate_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrCurrentTime_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrCurrentTime_DESKTOP", (typeof(bool), "IntegrationToggles") },

                { "IntgrSpotifyStatus_VR", (typeof(bool), "IntegrationToggles") },
                { "IntgrSpotifyStatus_DESKTOP", (typeof(bool), "IntegrationToggles") },
                { "Time24H", (typeof(bool), "Time") },
                { "PrefixTime", (typeof(bool), "Time") },
                { "TimeShowTimeZone", (typeof(bool), "Time") },
                { "SelectedTimeZone", (typeof(Timezone), "Time") },
                { "UseDaylightSavingTime", (typeof(bool), "Time") },
                { "AutoSetDaylight", (typeof(bool), "Time") },

                { "CurrentMenuItem", (typeof(int), "Menu") },

                { "IntgrScanForce", (typeof(bool), "WindowActivity") },
                { "AutoShowTitleOnNewApp", (typeof(bool), "WindowActivity") },
                { "WindowActivityTitleScan", (typeof(bool), "WindowActivity") },
                { "MaxShowTitleCount", (typeof(int), "WindowActivity") },
                { "LimitTitleOnApp", (typeof(bool), "WindowActivity") },
                { "TitleOnAppVR", (typeof(bool), "WindowActivity") },
                { "WindowActivityPrivateName", (typeof(string), "WindowActivity") },

                { "MediaSession_Timeout", (typeof(int), "MediaLink") },
                { "MediaSession_AutoSwitchSpawn", (typeof(bool), "MediaLink") },
                { "MediaSession_AutoSwitch", (typeof(bool), "MediaLink") },
                { "DisableMediaLink", (typeof(bool), "MediaLink") },

                { "ScanningInterval", (typeof(double), "Scanning") },
                { "ScanPauseTimeout", (typeof(int), "Scanning") },

                { "PrefixIconMusic", (typeof(bool), "Icons") },
                { "PauseIconMusic", (typeof(bool), "Icons") },
                { "PrefixIconStatus", (typeof(bool), "Icons") },

                { "PrefixChat", (typeof(bool), "Chat") },
                { "ChatFX", (typeof(bool), "Chat") },
                { "ChatLiveEdit", (typeof(bool), "Chat") },
                { "KeepUpdatingChat", (typeof(bool), "Chat") },
                { "ChatSendAgainFX", (typeof(bool), "Chat") },
                { "ChatAddSmallDelay", (typeof(bool), "Chat") },
                { "ChatAddSmallDelayTIME", (typeof(double), "Chat") },
                { "ChattingUpdateRate", (typeof(double), "Chat") },
                { "RealTimeChatEdit", (typeof(bool), "Chat") },

                { "SeperateWithENTERS", (typeof(bool), "Custom") },

                { "Topmost", (typeof(bool), "Window") },
                { "JoinedAlphaChannel", (typeof(bool), "Update") },
                { "CheckUpdateOnStartup", (typeof(bool), "Update") },

                { "TTSTikTokEnabled", (typeof(bool), "TTS") },
                { "TTSCutOff", (typeof(bool), "TTS") },
                { "AutoUnmuteTTS", (typeof(bool), "TTS") },
                { "ToggleVoiceWithV", (typeof(bool), "TTS") },
                { "TTSVolume", (typeof(float), "TTS") },
                { "RecentTikTokTTSVoice", (typeof(string), "TTS") },
                { "RecentPlayBackOutput", (typeof(string), "TTS") },
                { "TTSOnResendChat", (typeof(bool), "TTS") },


                { "OSCIP", (typeof(string), "OSC") },
                { "OSCPortOut", (typeof(int), "OSC") },
                { "SecOSC", (typeof(bool), "OSC") },
                { "SecOSCPort", (typeof(int), "OSC") },

                { "BlankEgg", (typeof(bool), "DEV") },
                { "Egg_Dev", (typeof(bool), "DEV") },

                { "PulsoidAccessTokenOAuthEncrypted", (typeof(string), "PulsoidConnector") },
                { "HeartRateScanInterval_v2", (typeof(int), "PulsoidConnector") },
                { "HeartRate", (typeof(int), "PulsoidConnector") },
                { "HeartRateLastUpdate", (typeof(DateTime), "PulsoidConnector") },
                { "ShowBPMSuffix", (typeof(bool), "PulsoidConnector") },
                { "ApplyHeartRateAdjustment", (typeof(bool), "PulsoidConnector") },
                { "HeartRateAdjustment", (typeof(int), "PulsoidConnector") },
                { "SmoothHeartRate_v1", (typeof(bool), "PulsoidConnector") },
                { "SmoothHeartRateTimeSpan", (typeof(int), "PulsoidConnector") },
                { "HeartRateTrendIndicatorSensitivity", (typeof(double), "PulsoidConnector") },
                { "ShowHeartRateTrendIndicator", (typeof(bool), "PulsoidConnector") },
                { "HeartRateTrendIndicatorSampleRate", (typeof(int), "PulsoidConnector") },
                { "HeartRateTitle", (typeof(bool), "PulsoidConnector") },
                { "PulsoidAuthConnected", (typeof(bool), "PulsoidConnector") },

                { "LowTemperatureThreshold", (typeof(int), "PulsoidConnector") },
                { "HighTemperatureThreshold", (typeof(int), "PulsoidConnector") },
                { "ShowTemperatureText", (typeof(bool), "PulsoidConnector") },
                { "LowHeartRateText", (typeof(string), "PulsoidConnector") },
                { "HighHeartRateText", (typeof(string), "PulsoidConnector") },
                { "MagicHeartRateIcons", (typeof(bool), "PulsoidConnector") },

                { "Settings_Status", (typeof(bool), "OptionsTabState") },
                { "Settings_HeartRate", (typeof(bool), "OptionsTabState") },
                { "Settings_Time", (typeof(bool), "OptionsTabState") },
                { "Settings_ComponentStats", (typeof(bool), "OptionsTabState") },
                { "Settings_Chatting", (typeof(bool), "OptionsTabState") },
                { "Settings_TTS", (typeof(bool), "OptionsTabState") },
                { "Settings_MediaLink", (typeof(bool), "OptionsTabState") },
                { "Settings_AppOptions", (typeof(bool), "OptionsTabState") },
                { "Settings_WindowActivity", (typeof(bool), "OptionsTabState") }
            };
        }

        private static void LoadSettingFromXML(
            XmlNode categoryNode,
            KeyValuePair<string, (Type type, string category)> setting,
            PropertyInfo property)
        {
            XmlNode settingNode = categoryNode.SelectSingleNode(setting.Key);
            if (settingNode != null && !string.IsNullOrEmpty(settingNode.InnerText))
            {
                object value = ConvertToType(setting.Value.type, settingNode.InnerText);
                property.SetValue(ViewModel.Instance, value);
            }
        }

        private static void SaveSettingToXML(
            XmlDocument xmlDoc,
            XmlNode categoryNode,
            KeyValuePair<string, (Type type, string category)> setting,
            PropertyInfo property)
        {
            object value = property.GetValue(ViewModel.Instance);
            if (value != null && !string.IsNullOrEmpty(value.ToString()))
            {
                XmlNode settingNode = xmlDoc.CreateElement(setting.Key);
                settingNode.InnerText = value.ToString();
                categoryNode.AppendChild(settingNode);
            }
        }

        public static async Task CheckForUpdateAndWait(bool checkagain = false)
        {
            ViewModel.Instance.VersionTxt = "Checking for updates...";
            ViewModel.Instance.VersionTxtColor = "#FBB644";
            ViewModel.Instance.VersionTxtUnderLine = false;
            if (checkagain == true)
            {
                await Task.Delay(1000);
            }
            while (isUpdateCheckRunning)
            {
                await Task.Delay(500);
            }

            isUpdateCheckRunning = true;

            await Task.Run(() => CheckForUpdate());

            isUpdateCheckRunning = false;
        }


        public static void CompareVersions()
        {
            try
            {
                var currentVersion = ViewModel.Instance.AppVersion.VersionNumber;
                var latestReleaseVersion = ViewModel.Instance.LatestReleaseVersion.VersionNumber;

                int compareWithLatestRelease = currentVersion.CompareTo(latestReleaseVersion);

                if (compareWithLatestRelease < 0)
                {
                    // If the latest release version is greater than the current version
                    ViewModel.Instance.VersionTxt = "Update now";
                    ViewModel.Instance.VersionTxtColor = "#FF8AFF04";
                    ViewModel.Instance.VersionTxtUnderLine = true;
                    ViewModel.Instance.CanUpdate = true;
                    ViewModel.Instance.CanUpdateLabel = true;
                    ViewModel.Instance.UpdateURL = ViewModel.Instance.LatestReleaseURL;
                    return;
                }

                if (ViewModel.Instance.JoinedAlphaChannel && ViewModel.Instance.PreReleaseVersion != null)
                {
                    var preReleaseVersion = ViewModel.Instance.PreReleaseVersion.VersionNumber;
                    int compareWithPreRelease = currentVersion.CompareTo(preReleaseVersion);

                    if (compareWithPreRelease < 0)
                    {
                        // If the pre-release version is greater than the current version and the user has joined the alpha channel
                        ViewModel.Instance.VersionTxt = "Install pre-release";
                        ViewModel.Instance.VersionTxtUnderLine = true;
                        ViewModel.Instance.VersionTxtColor = "#2FD9FF";
                        ViewModel.Instance.CanUpdate = true;
                        ViewModel.Instance.CanUpdateLabel = false;
                        ViewModel.Instance.UpdateURL = ViewModel.Instance.PreReleaseURL;
                        return;
                    }
                    else if (compareWithPreRelease == 0)
                    {
                        // If the pre-release version is equal to the current version and the user has joined the alpha channel
                        ViewModel.Instance.VersionTxt = "Up-to-date (pre-release)";
                        ViewModel.Instance.VersionTxtUnderLine = false;
                        ViewModel.Instance.VersionTxtColor = "#75D5FE";
                        ViewModel.Instance.CanUpdateLabel = false;
                        ViewModel.Instance.CanUpdate = false;
                        return;
                    }
                }

                // Check if a downgrade is needed
                if (!ViewModel.Instance.JoinedAlphaChannel &&
                    ViewModel.Instance.LatestReleaseVersion != null &&
                    currentVersion.CompareTo(ViewModel.Instance.LatestReleaseVersion.VersionNumber) > 0)
                {
                    // If the current version is a pre-release version and the user has opted out of the alpha channel
                    ViewModel.Instance.VersionTxt = "Downgrade now";
                    ViewModel.Instance.VersionTxtColor = "#FF8AFF04";
                    ViewModel.Instance.VersionTxtUnderLine = true;
                    ViewModel.Instance.CanUpdate = true;
                    ViewModel.Instance.CanUpdateLabel = false;
                    ViewModel.Instance.UpdateURL = ViewModel.Instance.LatestReleaseURL;
                    return;
                }

                // If no new update or pre-release is found
                ViewModel.Instance.VersionTxt = "You are up-to-date";
                ViewModel.Instance.VersionTxtUnderLine = false;
                ViewModel.Instance.VersionTxtColor = "#FF92CC90";
                ViewModel.Instance.CanUpdateLabel = false;
                ViewModel.Instance.CanUpdate = false;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }


        public static bool CreateIfMissing(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    return true;
                }
                return true;
            }
            catch (IOException ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }

        public static void LoadAppList()
        {
            try
            {
                if (File.Exists(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml"));
                    if(json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("AppHistory history is null, not problem :P");
                        ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                        return;
                    }
                    ViewModel.Instance.ScannedApps = JsonConvert.DeserializeObject<ObservableCollection<ProcessInfo>>(json);
                }
                else
                {
                    Logging.WriteInfo("AppHistory history has never been created, not problem :P");
                    if(ViewModel.Instance.ScannedApps == null)
                    {
                        ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (ViewModel.Instance.ScannedApps == null)
                {
                    ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadChatList()
        {
            try
            {
                if (File.Exists(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("LastMessages history is null, not problem :P");
                        ViewModel.Instance.LastMessages = new ObservableCollection<ChatItem>();
                        return;
                    }
                    ViewModel.Instance.LastMessages = JsonConvert.DeserializeObject<ObservableCollection<ChatItem>>(json);
                    foreach (var item in ViewModel.Instance.LastMessages)
                    {
                        item.CanLiveEdit = false;
                    }
                }
                else
                {
                    Logging.WriteInfo("LastMessages history has never been created, not problem :P");
                    if(ViewModel.Instance.LastMessages == null)
                    {
                        ViewModel.Instance.LastMessages = new ObservableCollection<ChatItem>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (ViewModel.Instance.ScannedApps == null)
                {
                    ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadMediaSessions()
        {
            try
            {
                if (File.Exists(Path.Combine(ViewModel.Instance.DataPath, "LastMediaLinkSessions.xml")))
                {
                    string json = File
                        .ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMediaLinkSessions.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                           Logging.WriteInfo("LastMediaLinkSessions history is null, not problem :P");
                        ViewModel.Instance.SavedSessionSettings = new List<MediaSessionSettings>();
                        return;
                    }
                    ViewModel.Instance.SavedSessionSettings = JsonConvert.DeserializeObject<List<MediaSessionSettings>>(json);
                }
                else
                {
                    Logging.WriteInfo("LastMediaSessions history has never been created, not problem :P");
                    if(ViewModel.Instance.SavedSessionSettings == null)
                    {
                        ViewModel.Instance.SavedSessionSettings = new List<MediaSessionSettings>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (ViewModel.Instance.ScannedApps == null)
                {
                    ViewModel.Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadStatusList()
        {
            try
            {
                if (File.Exists(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("StatusList history is null, not problem :P");
                        ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>();
                        return;
                    }
                    ViewModel.Instance.StatusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
                }
                else
                {
                    Random random = new Random();
                    int randomId = random.Next(10, 99999999);
                    ViewModel.Instance.StatusList
                        .Add(
                            new StatusItem
                            {
                                CreationDate = DateTime.Now,
                                IsActive = true,
                                IsFavorite = true,
                                msg = "Enjoy 💖",
                                MSGID = randomId
                            });
                    ViewModel.SaveStatusList();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (ViewModel.Instance.StatusList == null)
                {
                    ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>();
                }
            }
        }


        public static void ManageSettingsXML(bool saveSettings = false)
        {
            try
            {
                string datapath = Path.Combine(ViewModel.Instance.DataPath, "settings.xml");
                if (!CreateIfMissing(ViewModel.Instance.DataPath))
                    return;

                XmlDocument xmlDoc = new XmlDocument();
                XmlNode rootNode;

                if (saveSettings)
                {
                    rootNode = xmlDoc.CreateElement("Settings");
                    xmlDoc.AppendChild(rootNode);
                }
                else
                {
                    if (!File.Exists(datapath))
                    {
                        return;
                    }

                    xmlDoc.Load(datapath);
                    rootNode = xmlDoc.SelectSingleNode("Settings");
                }

                var settings = InitializeSettingsDictionary();

                foreach (var setting in settings)
                {
                    try
                    {
                        PropertyInfo property = ViewModel.Instance.GetType().GetProperty(setting.Key);
                        XmlNode categoryNode = GetOrCreateNode(xmlDoc, rootNode, setting.Value.category);

                        if (saveSettings)
                        {
                            SaveSettingToXML(xmlDoc, categoryNode, setting, property);
                        }
                        else
                        {
                            LoadSettingFromXML(categoryNode, setting, property);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                    }
                }

                if (saveSettings)
                {
                    xmlDoc.Save(datapath);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

        }

        public static bool PopulateOutputDevices(bool beforeTTS = false)
        {
            try
            {
                var devicesRen_enumerator = new MMDeviceEnumerator();
                var devicesRen = devicesRen_enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                var deviceNumber = 0;

                if (beforeTTS == true)
                {
                    ViewModel.Instance.PlaybackOutputDevices.Clear();
                }

                foreach (var device in devicesRen)
                {
                    ViewModel.Instance.PlaybackOutputDevices.Add(new AudioDevice(device.FriendlyName, device.ID, deviceNumber++));
                }

                var defaultPlaybackOutputDevice = devicesRen_enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia);
                if (ViewModel.Instance.RecentPlayBackOutput == null)
                {
                    ViewModel.Instance.SelectedPlaybackOutputDevice = new AudioDevice(
                        defaultPlaybackOutputDevice.FriendlyName,
                        defaultPlaybackOutputDevice.ID,
                        -1);
                    ViewModel.Instance.RecentPlayBackOutput = ViewModel.Instance.SelectedPlaybackOutputDevice.FriendlyName;
                }
                else
                {
                    AudioDevice ADevice = ViewModel.Instance.PlaybackOutputDevices
                        .FirstOrDefault(v => v.FriendlyName == ViewModel.Instance.RecentPlayBackOutput);
                    if (ADevice == null)
                    {
                        ViewModel.Instance.SelectedPlaybackOutputDevice = new AudioDevice(
                            defaultPlaybackOutputDevice.FriendlyName,
                            defaultPlaybackOutputDevice.ID,
                            -1);
                        ViewModel.Instance.RecentPlayBackOutput = ViewModel.Instance.SelectedPlaybackOutputDevice.FriendlyName;
                    }
                    else
                    {
                        ViewModel.Instance.SelectedPlaybackOutputDevice = ADevice;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return false;
            }
            return true;
        }

        // Check for updates

        public static List<Voice> ReadTkTkTTSVoices()
        {
            try
            {
                string json = File.ReadAllText(@"Json\voices.json");
                List<Voice> ConfirmList = JsonConvert.DeserializeObject<List<Voice>>(json);

                if (string.IsNullOrEmpty(ViewModel.Instance.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    ViewModel.Instance.RecentTikTokTTSVoice = "en_us_001";
                }
                if (!string.IsNullOrEmpty(ViewModel.Instance.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    Voice selectedVoice = ConfirmList.FirstOrDefault(v => v.ApiName == ViewModel.Instance.RecentTikTokTTSVoice);
                    if (selectedVoice == null)
                    {
                    }
                    else
                    {
                        ViewModel.Instance.SelectedTikTokTTSVoice = selectedVoice;
                    }
                }

                return ConfirmList;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return null;
            }
        }

        public static void SaveAppList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.ScannedApps);
                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        public static void SaveChatList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.LastMessages);
                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        public static void SaveMediaSessions()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.SavedSessionSettings);
                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMediaLinkSessions.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}
