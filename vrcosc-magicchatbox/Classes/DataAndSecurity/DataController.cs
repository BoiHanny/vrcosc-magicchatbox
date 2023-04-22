using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        public static void SaveSettingsToXML()
        {
            if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    XmlNode rootNode = xmlDoc.CreateElement("Settings");
                    xmlDoc.AppendChild(rootNode);

                    XmlNode userNode = xmlDoc.CreateElement("IntgrStatus");
                    userNode.InnerText = ViewModel.Instance.IntgrStatus.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanWindowActivity");
                    userNode.InnerText = ViewModel.Instance.IntgrScanWindowActivity.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanSpotify");
                    userNode.InnerText = ViewModel.Instance.IntgrScanSpotify.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanWindowTime");
                    userNode.InnerText = ViewModel.Instance.IntgrScanWindowTime.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixTime");
                    userNode.InnerText = ViewModel.Instance.PrefixTime.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OnlyShowTimeVR");
                    userNode.InnerText = ViewModel.Instance.OnlyShowTimeVR.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ScanInterval");
                    userNode.InnerText = ViewModel.Instance.ScanInterval.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCIP");
                    userNode.InnerText = ViewModel.Instance.OSCIP.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCPortOut");
                    userNode.InnerText = ViewModel.Instance.OSCPortOut.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("Time24H");
                    userNode.InnerText = ViewModel.Instance.Time24H.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("CurrentMenuItem");
                    userNode.InnerText = ViewModel.Instance.CurrentMenuItem.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixIconMusic");
                    userNode.InnerText = ViewModel.Instance.PrefixIconMusic.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixIconStatus");
                    userNode.InnerText = ViewModel.Instance.PrefixIconStatus.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ScanPauseTimeout");
                    userNode.InnerText = ViewModel.Instance.ScanPauseTimeout.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixChat");
                    userNode.InnerText = ViewModel.Instance.PrefixChat.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ChatFX");
                    userNode.InnerText = ViewModel.Instance.ChatFX.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PauseIconMusic");
                    userNode.InnerText = ViewModel.Instance.PauseIconMusic.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("Topmost");
                    userNode.InnerText = ViewModel.Instance.Topmost.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("RecentTikTokTTSVoice");
                    userNode.InnerText = ViewModel.Instance.RecentTikTokTTSVoice.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("TTSTikTokEnabled");
                    userNode.InnerText = ViewModel.Instance.TTSTikTokEnabled.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("TTSCutOff");
                    userNode.InnerText = ViewModel.Instance.TTSCutOff.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("RecentPlayBackOutput");
                    userNode.InnerText = ViewModel.Instance.RecentPlayBackOutput.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("AutoUnmuteTTS");
                    userNode.InnerText = ViewModel.Instance.AutoUnmuteTTS.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("TTSVolume");
                    userNode.InnerText = ViewModel.Instance.TTSVolume.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ToggleVoiceWithV");
                    userNode.InnerText = ViewModel.Instance.ToggleVoiceWithV.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OpenAIAPIKey");
                    userNode.InnerText = ViewModel.Instance.OpenAIAPIKey.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OpenAIAPISelectedModel");
                    userNode.InnerText = ViewModel.Instance.OpenAIAPISelectedModel.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OpenAIUsedTokens");
                    userNode.InnerText = ViewModel.Instance.OpenAIUsedTokens.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrIntelliWing");
                    userNode.InnerText = ViewModel.Instance.IntgrIntelliWing.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("GetForegroundProcessNew");
                    userNode.InnerText = ViewModel.Instance.GetForegroundProcessNew.ToString();
                    rootNode.AppendChild(userNode);

                    xmlDoc.Save(Path.Combine(ViewModel.Instance.DataPath, "settings.xml"));
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);

                }
            }



        }

        public static void LoadSettingsFromXML()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(ViewModel.Instance.DataPath, "settings.xml"));

                ViewModel.Instance.IntgrStatus = bool.Parse(doc.GetElementsByTagName("IntgrStatus")[0].InnerText);
                ViewModel.Instance.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                ViewModel.Instance.IntgrScanWindowActivity = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowActivity")[0].InnerText);
                ViewModel.Instance.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                ViewModel.Instance.IntgrScanWindowTime = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowTime")[0].InnerText);
                ViewModel.Instance.PrefixTime = bool.Parse(doc.GetElementsByTagName("PrefixTime")[0].InnerText);
                ViewModel.Instance.OnlyShowTimeVR = bool.Parse(doc.GetElementsByTagName("OnlyShowTimeVR")[0].InnerText);
                ViewModel.Instance.Time24H = bool.Parse(doc.GetElementsByTagName("Time24H")[0].InnerText);
                ViewModel.Instance.ScanInterval = int.Parse(doc.GetElementsByTagName("ScanInterval")[0].InnerText);
                ViewModel.Instance.CurrentMenuItem = int.Parse(doc.GetElementsByTagName("CurrentMenuItem")[0].InnerText);
                ViewModel.Instance.OSCIP = doc.GetElementsByTagName("OSCIP")[0].InnerText;
                ViewModel.Instance.OSCPortOut = int.Parse(doc.GetElementsByTagName("OSCPortOut")[0].InnerText);
                ViewModel.Instance.PrefixIconMusic = bool.Parse(doc.GetElementsByTagName("PrefixIconMusic")[0].InnerText);
                ViewModel.Instance.PrefixIconStatus = bool.Parse(doc.GetElementsByTagName("PrefixIconStatus")[0].InnerText);
                ViewModel.Instance.ScanPauseTimeout = int.Parse(doc.GetElementsByTagName("ScanPauseTimeout")[0].InnerText);
                ViewModel.Instance.PrefixChat = bool.Parse(doc.GetElementsByTagName("PrefixChat")[0].InnerText);
                ViewModel.Instance.ChatFX = bool.Parse(doc.GetElementsByTagName("ChatFX")[0].InnerText);
                ViewModel.Instance.PauseIconMusic = bool.Parse(doc.GetElementsByTagName("PauseIconMusic")[0].InnerText);
                ViewModel.Instance.Topmost = bool.Parse(doc.GetElementsByTagName("Topmost")[0].InnerText);
                ViewModel.Instance.RecentTikTokTTSVoice = doc.GetElementsByTagName("RecentTikTokTTSVoice")[0].InnerText;
                ViewModel.Instance.TTSTikTokEnabled = bool.Parse(doc.GetElementsByTagName("TTSTikTokEnabled")[0].InnerText);
                ViewModel.Instance.TTSCutOff = bool.Parse(doc.GetElementsByTagName("TTSCutOff")[0].InnerText);
                ViewModel.Instance.RecentPlayBackOutput = doc.GetElementsByTagName("RecentPlayBackOutput")[0].InnerText;
                ViewModel.Instance.AutoUnmuteTTS = bool.Parse(doc.GetElementsByTagName("AutoUnmuteTTS")[0].InnerText);
                ViewModel.Instance.TTSVolume = float.Parse(doc.GetElementsByTagName("TTSVolume")[0].InnerText);
                ViewModel.Instance.ToggleVoiceWithV = bool.Parse(doc.GetElementsByTagName("ToggleVoiceWithV")[0].InnerText);
                ViewModel.Instance.OpenAIAPIKey = doc.GetElementsByTagName("OpenAIAPIKey")[0].InnerText;
                ViewModel.Instance.OpenAIAPISelectedModel = doc.GetElementsByTagName("OpenAIAPISelectedModel")[0].InnerText;
                ViewModel.Instance.OpenAIUsedTokens = int.Parse(doc.GetElementsByTagName("OpenAIUsedTokens")[0].InnerText);
                ViewModel.Instance.IntgrIntelliWing = bool.Parse(doc.GetElementsByTagName("IntgrIntelliWing")[0].InnerText);
                ViewModel.Instance.GetForegroundProcessNew = bool.Parse(doc.GetElementsByTagName("GetForegroundProcessNew")[0].InnerText);
            }
            catch (Exception ex)
            {

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
