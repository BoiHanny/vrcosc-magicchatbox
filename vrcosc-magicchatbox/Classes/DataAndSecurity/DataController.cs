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

        public static async Task CheckForUpdateAndWait(bool checkagain = false)
        {
            if(checkagain == true)
            {
                Task.Delay(1000).Wait();
            }
            // Wait until previous check for updates is not running anymore
            while (isUpdateCheckRunning)
            {
                await Task.Delay(500); // Wait for 500 ms before checking again
            }

            // Lock the check for updates
            isUpdateCheckRunning = true;

            // Write your code to check for updates here
            CheckForUpdate();

            // Unlock the check for updates
            isUpdateCheckRunning = false;
        }

        public static List<Voice> ReadTkTkTTSVoices()
        {
            try
            {
                string json = System.IO.File.ReadAllText(@"Json\voices.json");
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

        public static void LoadIntelliChatBuiltInActions()
        {
            try
            {
                if (File.Exists(@"Json\\OpenAIAPIBuiltInActions.json"))
                {
                    string json = File.ReadAllText(@"Json\\OpenAIAPIBuiltInActions.json");
                    ViewModel.Instance.OpenAIAPIBuiltInActions = JsonConvert.DeserializeObject<ObservableCollection<ChatModelMsg>>(json);
                }
                else
                {
                    // Initialize PreCreatedActions with default actions or an empty list
                    ViewModel.Instance.OpenAIAPIBuiltInActions = new ObservableCollection<ChatModelMsg>();
                }
            }
            catch (Exception ex)
            {
                // Handle the exception, e.g., by logging it
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

                var defaultPlaybackOutputDevice = devicesRen_enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (ViewModel.Instance.RecentPlayBackOutput == null)
                {
                    ViewModel.Instance.SelectedPlaybackOutputDevice = new AudioDevice(defaultPlaybackOutputDevice.FriendlyName, defaultPlaybackOutputDevice.ID, -1);
                    ViewModel.Instance.RecentPlayBackOutput = ViewModel.Instance.SelectedPlaybackOutputDevice.FriendlyName;
                }
                else
                {
                    AudioDevice ADevice = ViewModel.Instance.PlaybackOutputDevices.FirstOrDefault(v => v.FriendlyName == ViewModel.Instance.RecentPlayBackOutput);
                    if (ADevice == null)
                    {
                        ViewModel.Instance.SelectedPlaybackOutputDevice = new AudioDevice(defaultPlaybackOutputDevice.FriendlyName, defaultPlaybackOutputDevice.ID, -1);
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




        public static void ManageSettingsXML(bool saveSettings = false)
        {
            if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    XmlNode rootNode;

                    if (saveSettings)
                    {
                        rootNode = xmlDoc.CreateElement("Settings");
                        xmlDoc.AppendChild(rootNode);
                    }
                    else
                    {
                        xmlDoc.Load(Path.Combine(ViewModel.Instance.DataPath, "settings.xml"));
                        rootNode = xmlDoc.SelectSingleNode("Settings");
                    }

                    var settings = new Dictionary<string, (Type type, string category)>()
                    {
                        {"IntgrStatus", (typeof(bool), "Integrations")},
                        {"IntgrScanWindowActivity", (typeof(bool), "Integrations")},
                        {"IntgrScanSpotify_OLD", (typeof(bool), "Integrations")},
                        {"IntgrScanWindowTime", (typeof(bool), "Integrations")},
                        {"IntgrIntelliWing", (typeof(bool), "Integrations")},
                        {"ApplicationHookV2", (typeof(bool), "Integrations")},
                        {"IntgrHeartRate", (typeof(bool), "Integrations")},
                        {"IntgrScanMediaLink", (typeof(bool), "Integrations")},

                        {"IntgrStatus_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrStatus_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"IntgrMediaLink_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrMediaLink_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"IntgrWindowActivity_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrWindowActivity_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"IntgrHeartRate_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrHeartRate_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"IntgrCurrentTime_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrCurrentTime_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"IntgrSpotifyStatus_VR", (typeof(bool), "IntegrationToggles")},
                        {"IntgrSpotifyStatus_DESKTOP", (typeof(bool), "IntegrationToggles")},

                        {"Time24H", (typeof(bool), "Time")},
                        {"PrefixTime", (typeof(bool), "Time")},
                        {"TimeShowTimeZone", (typeof(bool), "Time")},
                        {"SelectedTimeZone", (typeof(Timezone), "Time")},
                        {"UseDaylightSavingTime", (typeof(bool), "Time")},
                        {"AutoSetDaylight", (typeof(bool), "Time")},

                        {"CurrentMenuItem", (typeof(int), "Menu")},

                        {"MediaSession_Timeout", (typeof(int), "MediaLink")},
                        {"MediaSession_AutoSwitchSpawn", (typeof(bool), "MediaLink")},
                        {"MediaSession_AutoSwitch", (typeof(bool), "MediaLink")},
                        {"DisableMediaLink", (typeof(bool), "MediaLink")},

                        {"ScanInterval", (typeof(int), "Scanning")},
                        {"ScanPauseTimeout", (typeof(int), "Scanning")},

                        {"PrefixIconMusic", (typeof(bool), "Icons")},
                        {"PauseIconMusic", (typeof(bool), "Icons")},
                        {"PrefixIconStatus", (typeof(bool), "Icons")},

                        {"PrefixChat", (typeof(bool), "Chat")},
                        {"ChatFX", (typeof(bool), "Chat")},

                        {"SeperateWithENTERS", (typeof(bool), "Custom")},

                        {"Topmost", (typeof(bool), "Window")},
                        {"JoinedAlphaChannel", (typeof(bool), "Update")},

                        {"TTSTikTokEnabled", (typeof(bool), "TTS")},
                        {"TTSCutOff", (typeof(bool), "TTS")},
                        {"AutoUnmuteTTS", (typeof(bool), "TTS")},
                        {"ToggleVoiceWithV", (typeof(bool), "TTS")},
                        {"TTSVolume", (typeof(float), "TTS")},
                        {"RecentTikTokTTSVoice", (typeof(string), "TTS")},
                        {"RecentPlayBackOutput", (typeof(string), "TTS")},

                        {"OpenAIAPIKey", (typeof(string), "OpenAI")},
                        {"OpenAIAPISelectedModel", (typeof(string), "OpenAI")},
                        {"OpenAIUsedTokens", (typeof(int), "OpenAI")},

                        {"OSCIP", (typeof(string), "OSC")},
                        {"OSCPortOut", (typeof(int), "OSC")},
                        {"SecOSC", (typeof(string), "OSC")},
                        {"SecOSCPort", (typeof(int), "OSC")},

                        {"BlankEgg", (typeof(bool), "DEV")},
                        {"Egg_Dev", (typeof(bool), "DEV")},

                        {"PulsoidAccessToken", (typeof(string), "HeartRateConnector")},
                        {"HeartRateScanInterval", (typeof(int), "HeartRateConnector")},
                        {"HeartRate", (typeof(int), "HeartRateConnector")},
                        {"HeartRateLastUpdate", (typeof(DateTime), "HeartRateConnector")},
                        {"ShowBPMSuffix", (typeof(bool), "HeartRateConnector")},
                        {"ApplyHeartRateAdjustment", (typeof(bool), "HeartRateConnector")},
                        {"HeartRateAdjustment", (typeof(int), "HeartRateConnector")},
                        {"SmoothHeartRate", (typeof(bool), "HeartRateConnector")},
                        {"HeartRateTimeSpan", (typeof(int), "HeartRateConnector")},
                        {"HeartRateTrendIndicatorSensitivity", (typeof(double), "HeartRateConnector")},
                        {"ShowHeartRateTrendIndicator", (typeof(bool), "HeartRateConnector")},
                        {"HeartRateTrendIndicatorSampleRate", (typeof(int), "HeartRateConnector")},


                        {"Settings_Status", (typeof(bool), "OptionsTabState")},
                        {"Settings_HeartRate", (typeof(bool), "OptionsTabState")},
                        {"Settings_Time", (typeof(bool), "OptionsTabState")},
                        {"Settings_Chatting", (typeof(bool), "OptionsTabState")},
                        {"Settings_TTS", (typeof(bool), "OptionsTabState")},
                        {"Settings_MediaLink", (typeof(bool), "OptionsTabState")},
                        {"Settings_IntelliChat", (typeof(bool), "OptionsTabState")},
                        {"Settings_AppOptions", (typeof(bool), "OptionsTabState")},
                        {"Settings_WindowActivity", (typeof(bool), "OptionsTabState")}



                    };

                    foreach (var setting in settings)
                    {
                        try
                        {
                            PropertyInfo property = ViewModel.Instance.GetType().GetProperty(setting.Key);
                            XmlNode categoryNode = rootNode.SelectSingleNode(setting.Value.category);

                            if (categoryNode == null)
                            {
                                categoryNode = xmlDoc.CreateElement(setting.Value.category);
                                rootNode.AppendChild(categoryNode);
                            }

                            if (saveSettings)
                            {
                                object value = property.GetValue(ViewModel.Instance);
                                if (value != null && !string.IsNullOrEmpty(value.ToString()))
                                {
                                    XmlNode settingNode = xmlDoc.CreateElement(setting.Key);
                                    settingNode.InnerText = value.ToString();
                                    categoryNode.AppendChild(settingNode);
                                }
                            }
                            else
                            {
                                XmlNode settingNode = categoryNode.SelectSingleNode(setting.Key);

                                if (settingNode != null && !string.IsNullOrEmpty(settingNode.InnerText))
                                {
                                    if (setting.Value.type == typeof(bool))
                                    {
                                        property.SetValue(ViewModel.Instance, bool.Parse(settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(int))
                                    {
                                        property.SetValue(ViewModel.Instance, int.Parse(settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(string))
                                    {
                                        property.SetValue(ViewModel.Instance, settingNode.InnerText);
                                    }
                                    else if (setting.Value.type == typeof(float))
                                    {
                                        property.SetValue(ViewModel.Instance, float.Parse(settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(double))
                                    {
                                        property.SetValue(ViewModel.Instance, double.Parse(settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(Timezone))
                                    {
                                        property.SetValue(ViewModel.Instance, Enum.Parse(typeof(Timezone), settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(DateTime))
                                    {
                                        property.SetValue(ViewModel.Instance, DateTime.Parse(settingNode.InnerText));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (saveSettings)
                    {
                        xmlDoc.Save(Path.Combine(ViewModel.Instance.DataPath, "settings.xml"));
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }







        public static void LoadChatList()
        {
            try
            {
                if (System.IO.File.Exists(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml")))
                {
                    string json = System.IO.File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml"));
                    ViewModel.Instance.LastMessages = JsonConvert.DeserializeObject<ObservableCollection<ChatItem>>(json);
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }

        }

        public static void SaveChatList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.LastMessages);
                    System.IO.File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml"), json);
                }

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

        }

        public static void LoadAppList()
        {
            try
            {
                if (System.IO.File.Exists(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml")))
                {
                    string json = System.IO.File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml"));
                    ViewModel.Instance.ScannedApps = JsonConvert.DeserializeObject<ObservableCollection<ProcessInfo>>(json);
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }

        }

        public static void SaveAppList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.ScannedApps);
                    System.IO.File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml"), json);
                }

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

        }


        public static void LoadStatusList()
        {
            if (System.IO.File.Exists(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml")))
            {
                string json = System.IO.File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml"));
                ViewModel.Instance.StatusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
            }
            else
            {
                Random random = new Random();
                int randomId = random.Next(10, 99999999);
                ViewModel.Instance.StatusList.Add(new StatusItem { CreationDate = DateTime.Now, IsActive = true, IsFavorite = true, msg = "Bubs", MSGLenght = 4, MSGID = randomId });
                ViewModel.Instance.StatusList.Add(new StatusItem { CreationDate = DateTime.Now, IsActive = false, IsFavorite = true, msg = "Enjoy <$", MSGLenght = 8, MSGID = randomId });
                ViewModel.SaveStatusList();
            }
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

                        ViewModel.Instance.LatestReleaseVersion = new Version(Regex.Replace(latestVersion, "[^0-9.]", ""));
                        ViewModel.Instance.LatestReleaseURL = releaseLatest.assets[0].browser_download_url; // Store the download URL

                        // Check the latest pre-release
                        var responsePreRelease = client.GetAsync(urlPreRelease).Result;
                        var jsonPreRelease = responsePreRelease.Content.ReadAsStringAsync().Result;
                        JArray releases = JArray.Parse(jsonPreRelease);
                        string preReleaseVersion = "";
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
                            ViewModel.Instance.PreReleaseVersion = new Version(Regex.Replace(preReleaseVersion, "[^0-9.]", ""));
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
            }
            finally
            {
                CompareVersions();
            }
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
                    ViewModel.Instance.CanUpdate = true;
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
                        ViewModel.Instance.VersionTxtColor = "#FF8AFF04";
                        ViewModel.Instance.CanUpdate = true;
                        ViewModel.Instance.UpdateURL = ViewModel.Instance.PreReleaseURL;
                        return;
                    }
                }

                // Check if a downgrade is needed
                if (!ViewModel.Instance.JoinedAlphaChannel && ViewModel.Instance.LatestReleaseVersion != null &&
                    currentVersion.CompareTo(ViewModel.Instance.LatestReleaseVersion.VersionNumber) > 0)
                {
                    // If the current version is a pre-release version and the user has opted out of the alpha channel
                    ViewModel.Instance.VersionTxt = "Downgrade now";
                    ViewModel.Instance.VersionTxtColor = "#FF8AFF04";
                    ViewModel.Instance.CanUpdate = true;
                    ViewModel.Instance.UpdateURL = ViewModel.Instance.LatestReleaseURL;
                    return;
                }

                // If no new update or pre-release is found
                ViewModel.Instance.VersionTxt = "You are up-to-date";
                ViewModel.Instance.VersionTxtColor = "#FF92CC90";
                ViewModel.Instance.CanUpdate = false;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }



    }
}
