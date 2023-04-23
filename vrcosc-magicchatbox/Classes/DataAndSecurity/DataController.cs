using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using Version = vrcosc_magicchatbox.ViewModels.Version;

namespace vrcosc_magicchatbox.DataAndSecurity
{
    internal static class DataController
    {

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
                        {"IntgrScanSpotify", (typeof(bool), "Integrations")},
                        {"IntgrScanWindowTime", (typeof(bool), "Integrations")},
                        {"IntgrIntelliWing", (typeof(bool), "Integrations")},
                        {"ApplicationHookV2", (typeof(bool), "Integrations")},

                        {"Time24H", (typeof(bool), "Time")},
                        {"OnlyShowTimeVR", (typeof(bool), "Time")},
                        {"PrefixTime", (typeof(bool), "Time")},

                        {"CurrentMenuItem", (typeof(int), "Menu")},

                        {"ScanInterval", (typeof(int), "Scanning")},
                        {"ScanPauseTimeout", (typeof(int), "Scanning")},

                        {"PrefixIconMusic", (typeof(bool), "Icons")},
                        {"PauseIconMusic", (typeof(bool), "Icons")},
                        {"PrefixIconStatus", (typeof(bool), "Icons")},

                        {"PrefixChat", (typeof(bool), "Chat")},
                        {"ChatFX", (typeof(bool), "Chat")},

                        {"Topmost", (typeof(bool), "Window")},

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
                                    else if (setting.Value.type == typeof(float))
                                    {
                                        property.SetValue(ViewModel.Instance, float.Parse(settingNode.InnerText));
                                    }
                                    else if (setting.Value.type == typeof(string))
                                    {
                                        property.SetValue(ViewModel.Instance, settingNode.InnerText);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the exception or handle it as needed
                        }
                    }

                    if (saveSettings)
                    {
                        xmlDoc.Save(Path.Combine(ViewModel.Instance.DataPath, "settings.xml"));
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it as needed
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

        public static void CheckForUpdate()
        {
            try
            {
                string token = EncryptionMethods.DecryptString(ViewModel.Instance.ApiStream);
                string url = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases/latest";
                if (url != null)
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                        client.DefaultRequestHeaders.Add("User-Agent", "vrcosc-magicchatbox-update-checker");
                        var response = client.GetAsync(url).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        dynamic release = JsonConvert.DeserializeObject(json);
                        string latestVersion = release.tag_name;
                        string tagURL = "https://github.com/BoiHanny/vrcosc-magicchatbox/releases/tag/" + latestVersion;
                        ViewModel.Instance.GitHubVersion = new Version(Regex.Replace(latestVersion, "[^0-9.]", ""));
                        if (ViewModel.Instance.GitHubVersion != null)
                        {
                            CompareVersions();
                            ViewModel.Instance.NewVersionURL = release.assets[0].browser_download_url; // Store the download URL
                            ViewModel.Instance.tagURL = tagURL;
                        }
                        else
                        {
                            ViewModel.Instance.VersionTxt = "Internal update server error";
                            ViewModel.Instance.VersionTxtColor = "#F65F69";
                            Logging.WriteInfo("Internal update server error", makeVMDump: true, MSGBox: false);
                            ViewModel.Instance.CanUpdate = false;
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

        }


        public static void CompareVersions()
        {

            try
            {
                var currentVersion = ViewModel.Instance.AppVersion.VersionNumber; ;
                var githubVersion = ViewModel.Instance.GitHubVersion.VersionNumber;

                int result = currentVersion.CompareTo(githubVersion);
                if (result < 0)
                {
                    ViewModel.Instance.VersionTxt = "Update now";
                    ViewModel.Instance.VersionTxtColor = "#FF8AFF04";
                    ViewModel.Instance.CanUpdate = true;
                }
                else if (result == 0)
                {
                    ViewModel.Instance.VersionTxt = "You are up-to-date";
                    ViewModel.Instance.VersionTxtColor = "#FF92CC90";
                    ViewModel.Instance.CanUpdate = false;
                }
                else
                {
                    ViewModel.Instance.VersionTxt = "You running a preview, fun!";
                    ViewModel.Instance.VersionTxtColor = "#FFE816EA";
                    ViewModel.Instance.CanUpdate = false;
                }

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}
