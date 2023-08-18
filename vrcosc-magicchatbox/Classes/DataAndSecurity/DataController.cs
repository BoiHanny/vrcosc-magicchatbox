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
using static vrcosc_magicchatbox.ViewModels.ViewModel;
using Version = vrcosc_magicchatbox.ViewModels.Version;

namespace vrcosc_magicchatbox.DataAndSecurity
{
    public static class DataController
    {
        private static bool isUpdateCheckRunning = false;

        private static void CheckForUpdate()
        {
            try
            {
                string token = EncryptionMethods.DecryptString(Instance.ApiStream);
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

                        Instance.LatestReleaseVersion = new Version(
                            Regex.Replace(latestVersion, "[^0-9.]", string.Empty));
                        Instance.LatestReleaseURL = releaseLatest.assets[0].browser_download_url; // Store the download URL

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
                        if (Instance.JoinedAlphaChannel && !string.IsNullOrEmpty(preReleaseVersion))
                        {
                            Instance.PreReleaseVersion = new Version(
                                Regex.Replace(preReleaseVersion, "[^0-9.]", string.Empty));
                            Instance.PreReleaseURL = releases[0]["assets"][0]["browser_download_url"].ToString(); // Store the download URL
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                Instance.VersionTxt = "Can't check updates";
                Instance.VersionTxtColor = "#F36734";
                Instance.VersionTxtUnderLine = false;
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

                { "PulsoidAccessToken", (typeof(string), "HeartRateConnector") },
                { "HeartRateScanInterval_v1", (typeof(int), "HeartRateConnector") },
                { "HeartRate", (typeof(int), "HeartRateConnector") },
                { "HeartRateLastUpdate", (typeof(DateTime), "HeartRateConnector") },
                { "ShowBPMSuffix", (typeof(bool), "HeartRateConnector") },
                { "ApplyHeartRateAdjustment", (typeof(bool), "HeartRateConnector") },
                { "HeartRateAdjustment", (typeof(int), "HeartRateConnector") },
                { "SmoothHeartRate_v1", (typeof(bool), "HeartRateConnector") },
                { "SmoothHeartRateTimeSpan", (typeof(int), "HeartRateConnector") },
                { "HeartRateTrendIndicatorSensitivity", (typeof(double), "HeartRateConnector") },
                { "ShowHeartRateTrendIndicator", (typeof(bool), "HeartRateConnector") },
                { "HeartRateTrendIndicatorSampleRate", (typeof(int), "HeartRateConnector") },
                { "HeartRateTitle", (typeof(bool), "HeartRateConnector") },


                { "Settings_Status", (typeof(bool), "OptionsTabState") },
                { "Settings_HeartRate", (typeof(bool), "OptionsTabState") },
                { "Settings_Time", (typeof(bool), "OptionsTabState") },
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
                property.SetValue(Instance, value);
            }
        }

        private static void SaveSettingToXML(
            XmlDocument xmlDoc,
            XmlNode categoryNode,
            KeyValuePair<string, (Type type, string category)> setting,
            PropertyInfo property)
        {
            object value = property.GetValue(Instance);
            if (value != null && !string.IsNullOrEmpty(value.ToString()))
            {
                XmlNode settingNode = xmlDoc.CreateElement(setting.Key);
                settingNode.InnerText = value.ToString();
                categoryNode.AppendChild(settingNode);
            }
        }

        public static async Task CheckForUpdateAndWait(bool checkagain = false)
        {
            Instance.VersionTxt = "Checking for updates...";
            Instance.VersionTxtColor = "#FBB644";
            Instance.VersionTxtUnderLine = false;
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
                var currentVersion = Instance.AppVersion.VersionNumber;
                var latestReleaseVersion = Instance.LatestReleaseVersion.VersionNumber;

                int compareWithLatestRelease = currentVersion.CompareTo(latestReleaseVersion);

                if (compareWithLatestRelease < 0)
                {
                    // If the latest release version is greater than the current version
                    Instance.VersionTxt = "Update now";
                    Instance.VersionTxtColor = "#FF8AFF04";
                    Instance.VersionTxtUnderLine = true;
                    Instance.CanUpdate = true;
                    Instance.CanUpdateLabel = true;
                    Instance.UpdateURL = Instance.LatestReleaseURL;
                    return;
                }

                if (Instance.JoinedAlphaChannel && Instance.PreReleaseVersion != null)
                {
                    var preReleaseVersion = Instance.PreReleaseVersion.VersionNumber;
                    int compareWithPreRelease = currentVersion.CompareTo(preReleaseVersion);

                    if (compareWithPreRelease < 0)
                    {
                        // If the pre-release version is greater than the current version and the user has joined the alpha channel
                        Instance.VersionTxt = "Install pre-release";
                        Instance.VersionTxtUnderLine = true;
                        Instance.VersionTxtColor = "#2FD9FF";
                        Instance.CanUpdate = true;
                        Instance.CanUpdateLabel = false;
                        Instance.UpdateURL = Instance.PreReleaseURL;
                        return;
                    }
                    else if (compareWithPreRelease == 0)
                    {
                        // If the pre-release version is equal to the current version and the user has joined the alpha channel
                        Instance.VersionTxt = "Up-to-date (pre-release)";
                        Instance.VersionTxtUnderLine = false;
                        Instance.VersionTxtColor = "#75D5FE";
                        Instance.CanUpdateLabel = false;
                        Instance.CanUpdate = false;
                        return;
                    }
                }

                // Check if a downgrade is needed
                if (!Instance.JoinedAlphaChannel &&
                    Instance.LatestReleaseVersion != null &&
                    currentVersion.CompareTo(Instance.LatestReleaseVersion.VersionNumber) > 0)
                {
                    // If the current version is a pre-release version and the user has opted out of the alpha channel
                    Instance.VersionTxt = "Downgrade now";
                    Instance.VersionTxtColor = "#FF8AFF04";
                    Instance.VersionTxtUnderLine = true;
                    Instance.CanUpdate = true;
                    Instance.CanUpdateLabel = false;
                    Instance.UpdateURL = Instance.LatestReleaseURL;
                    return;
                }

                // If no new update or pre-release is found
                Instance.VersionTxt = "You are up-to-date";
                Instance.VersionTxtUnderLine = false;
                Instance.VersionTxtColor = "#FF92CC90";
                Instance.CanUpdateLabel = false;
                Instance.CanUpdate = false;
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
                if (File.Exists(Path.Combine(Instance.DataPath, "AppHistory.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(Instance.DataPath, "AppHistory.xml"));
                    if(json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("AppHistory history is null, not problem :P");
                        Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                        return;
                    }
                    Instance.ScannedApps = JsonConvert.DeserializeObject<ObservableCollection<ProcessInfo>>(json);
                }
                else
                {
                    Logging.WriteInfo("AppHistory history has never been created, not problem :P");
                    if(Instance.ScannedApps == null)
                    {
                        Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (Instance.ScannedApps == null)
                {
                    Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadChatList()
        {
            try
            {
                if (File.Exists(Path.Combine(Instance.DataPath, "LastMessages.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(Instance.DataPath, "LastMessages.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("LastMessages history is null, not problem :P");
                        Instance.LastMessages = new ObservableCollection<ChatItem>();
                        return;
                    }
                    Instance.LastMessages = JsonConvert.DeserializeObject<ObservableCollection<ChatItem>>(json);
                    foreach (var item in Instance.LastMessages)
                    {
                        item.CanLiveEdit = false;
                    }
                }
                else
                {
                    Logging.WriteInfo("LastMessages history has never been created, not problem :P");
                    if(Instance.LastMessages == null)
                    {
                        Instance.LastMessages = new ObservableCollection<ChatItem>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (Instance.ScannedApps == null)
                {
                    Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadMediaSessions()
        {
            try
            {
                if (File.Exists(Path.Combine(Instance.DataPath, "LastMediaLinkSessions.xml")))
                {
                    string json = File
                        .ReadAllText(Path.Combine(Instance.DataPath, "LastMediaLinkSessions.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                           Logging.WriteInfo("LastMediaLinkSessions history is null, not problem :P");
                        Instance.SavedSessionSettings = new List<MediaSessionSettings>();
                        return;
                    }    
                    Instance.SavedSessionSettings = JsonConvert.DeserializeObject<List<MediaSessionSettings>>(json);
                }
                else
                {
                    Logging.WriteInfo("LastMediaSessions history has never been created, not problem :P");
                    if(Instance.SavedSessionSettings == null)
                    {
                        Instance.SavedSessionSettings = new List<MediaSessionSettings>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (Instance.ScannedApps == null)
                {
                    Instance.ScannedApps = new ObservableCollection<ProcessInfo>();
                }
            }
        }


        public static void LoadStatusList()
        {
            try
            {
                if (File.Exists(Path.Combine(Instance.DataPath, "StatusList.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(Instance.DataPath, "StatusList.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("StatusList history is null, not problem :P");
                        Instance.StatusList = new ObservableCollection<StatusItem>();
                        return;
                    }
                    Instance.StatusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
                }
                else
                {
                    Random random = new Random();
                    int randomId = random.Next(10, 99999999);
                    Instance.StatusList
                        .Add(
                            new StatusItem
                            {
                                CreationDate = DateTime.Now,
                                IsActive = true,
                                IsFavorite = true,
                                msg = "Enjoy 💖",
                                MSGID = randomId
                            });
                    SaveStatusList();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                if (Instance.StatusList == null)
                {
                    Instance.StatusList = new ObservableCollection<StatusItem>();
                }
            }
        }


        public static void ManageSettingsXML(bool saveSettings = false)
        {
            if (!CreateIfMissing(Instance.DataPath))
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
                xmlDoc.Load(Path.Combine(Instance.DataPath, "settings.xml"));
                rootNode = xmlDoc.SelectSingleNode("Settings");
            }

            var settings = InitializeSettingsDictionary();

            foreach (var setting in settings)
            {
                try
                {
                    PropertyInfo property = Instance.GetType().GetProperty(setting.Key);
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
                xmlDoc.Save(Path.Combine(Instance.DataPath, "settings.xml"));
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
                    Instance.PlaybackOutputDevices.Clear();
                }

                foreach (var device in devicesRen)
                {
                    Instance.PlaybackOutputDevices.Add(new AudioDevice(device.FriendlyName, device.ID, deviceNumber++));
                }

                var defaultPlaybackOutputDevice = devicesRen_enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia);
                if (Instance.RecentPlayBackOutput == null)
                {
                    Instance.SelectedPlaybackOutputDevice = new AudioDevice(
                        defaultPlaybackOutputDevice.FriendlyName,
                        defaultPlaybackOutputDevice.ID,
                        -1);
                    Instance.RecentPlayBackOutput = Instance.SelectedPlaybackOutputDevice.FriendlyName;
                }
                else
                {
                    AudioDevice ADevice = Instance.PlaybackOutputDevices
                        .FirstOrDefault(v => v.FriendlyName == Instance.RecentPlayBackOutput);
                    if (ADevice == null)
                    {
                        Instance.SelectedPlaybackOutputDevice = new AudioDevice(
                            defaultPlaybackOutputDevice.FriendlyName,
                            defaultPlaybackOutputDevice.ID,
                            -1);
                        Instance.RecentPlayBackOutput = Instance.SelectedPlaybackOutputDevice.FriendlyName;
                    }
                    else
                    {
                        Instance.SelectedPlaybackOutputDevice = ADevice;
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

                if (string.IsNullOrEmpty(Instance.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    Instance.RecentTikTokTTSVoice = "en_us_001";
                }
                if (!string.IsNullOrEmpty(Instance.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    Voice selectedVoice = ConfirmList.FirstOrDefault(v => v.ApiName == Instance.RecentTikTokTTSVoice);
                    if (selectedVoice == null)
                    {
                    }
                    else
                    {
                        Instance.SelectedTikTokTTSVoice = selectedVoice;
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
                if (CreateIfMissing(Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(Instance.ScannedApps);
                    File.WriteAllText(Path.Combine(Instance.DataPath, "AppHistory.xml"), json);
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
                if (CreateIfMissing(Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(Instance.LastMessages);
                    File.WriteAllText(Path.Combine(Instance.DataPath, "LastMessages.xml"), json);
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
                if (CreateIfMissing(Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(Instance.SavedSessionSettings);
                    File.WriteAllText(Path.Combine(Instance.DataPath, "LastMediaLinkSessions.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}
