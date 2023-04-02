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
    internal class DataController
    {
        private ViewModel _VM;
        private EncryptionMethods _ENCRY;
        public DataController(ViewModel vm)
        {
            _VM = vm;
            _ENCRY = new EncryptionMethods(_VM);
        }

        public List<Voice> ReadTkTkTTSVoices()
        {
            try
            {
                string json = File.ReadAllText(@"Json\voices.json");
                List<Voice> ConfirmList = JsonConvert.DeserializeObject<List<Voice>>(json);

                if (string.IsNullOrEmpty(_VM.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    _VM.RecentTikTokTTSVoice = "en_us_001";
                }
                if (!string.IsNullOrEmpty(_VM.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
                {
                    Voice selectedVoice = ConfirmList.FirstOrDefault(v => v.ApiName == _VM.RecentTikTokTTSVoice);
                    if (selectedVoice == null)
                    {
                    }
                    else
                    {
                        _VM.SelectedTikTokTTSVoice = selectedVoice;
                    }
                }

                return ConfirmList;
            }
            catch (Exception)
            {
                return null;
            }

        }

        public bool PopulateOutputDevices(bool beforeTTS= false)
        {
            try
            {
                var devicesRen_enumerator = new MMDeviceEnumerator();
                var devicesRen = devicesRen_enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                var deviceNumber = 0;

                if (beforeTTS == true)
                {
                    _VM.PlaybackOutputDevices.Clear();
                }

                foreach (var device in devicesRen)
                {
                    _VM.PlaybackOutputDevices.Add(new AudioDevice(device.FriendlyName, device.ID, deviceNumber++));
                }

                var defaultPlaybackOutputDevice = devicesRen_enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_VM.RecentPlayBackOutput == null)
                {
                    _VM.SelectedPlaybackOutputDevice = new AudioDevice(defaultPlaybackOutputDevice.FriendlyName, defaultPlaybackOutputDevice.ID, -1);
                    _VM.RecentPlayBackOutput = _VM.SelectedPlaybackOutputDevice.FriendlyName;
                }
                else
                {
                    AudioDevice ADevice = _VM.PlaybackOutputDevices.FirstOrDefault(v => v.FriendlyName == _VM.RecentPlayBackOutput);
                    if (ADevice == null)
                    {
                        _VM.SelectedPlaybackOutputDevice = new AudioDevice(defaultPlaybackOutputDevice.FriendlyName, defaultPlaybackOutputDevice.ID, -1);
                        _VM.RecentPlayBackOutput = _VM.SelectedPlaybackOutputDevice.FriendlyName;
                    }
                    else
                    {
                        _VM.SelectedPlaybackOutputDevice = ADevice;
                        
                    }
                }
            }
            catch (Exception ex)
            {
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
                return false;
            }

        }
        public void SaveSettingsToXML()
        {
            if (CreateIfMissing(_VM.DataPath) == true)
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    XmlNode rootNode = xmlDoc.CreateElement("Settings");
                    xmlDoc.AppendChild(rootNode);

                    XmlNode userNode = xmlDoc.CreateElement("IntgrStatus");
                    userNode.InnerText = _VM.IntgrStatus.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanWindowActivity");
                    userNode.InnerText = _VM.IntgrScanWindowActivity.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanSpotify");
                    userNode.InnerText = _VM.IntgrScanSpotify.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("IntgrScanWindowTime");
                    userNode.InnerText = _VM.IntgrScanWindowTime.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixTime");
                    userNode.InnerText = _VM.PrefixTime.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OnlyShowTimeVR");
                    userNode.InnerText = _VM.OnlyShowTimeVR.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ScanInterval");
                    userNode.InnerText = _VM.ScanInterval.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCIP");
                    userNode.InnerText = _VM.OSCIP.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("OSCPortOut");
                    userNode.InnerText = _VM.OSCPortOut.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("Time24H");
                    userNode.InnerText = _VM.Time24H.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("CurrentMenuItem");
                    userNode.InnerText = _VM.CurrentMenuItem.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixIconMusic");
                    userNode.InnerText = _VM.PrefixIconMusic.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixIconStatus");
                    userNode.InnerText = _VM.PrefixIconStatus.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ScanPauseTimeout");
                    userNode.InnerText = _VM.ScanPauseTimeout.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PrefixChat");
                    userNode.InnerText = _VM.PrefixChat.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("ChatFX");
                    userNode.InnerText = _VM.ChatFX.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("PauseIconMusic");
                    userNode.InnerText = _VM.PauseIconMusic.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("Topmost");
                    userNode.InnerText = _VM.Topmost.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("RecentTikTokTTSVoice");
                    userNode.InnerText = _VM.RecentTikTokTTSVoice.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("TTSTikTokEnabled");
                    userNode.InnerText = _VM.TTSTikTokEnabled.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("TTSCutOff");
                    userNode.InnerText = _VM.TTSCutOff.ToString();
                    rootNode.AppendChild(userNode);

                    userNode = xmlDoc.CreateElement("RecentPlayBackOutput");
                    userNode.InnerText = _VM.RecentPlayBackOutput.ToString();
                    rootNode.AppendChild(userNode);

                    xmlDoc.Save(Path.Combine(_VM.DataPath, "settings.xml"));
                }
                catch (Exception)
                {


                }
            }



        }

        public void LoadSettingsFromXML()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(_VM.DataPath, "settings.xml"));

                _VM.IntgrStatus = bool.Parse(doc.GetElementsByTagName("IntgrStatus")[0].InnerText);
                _VM.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                _VM.IntgrScanWindowActivity = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowActivity")[0].InnerText);
                _VM.IntgrScanSpotify = bool.Parse(doc.GetElementsByTagName("IntgrScanSpotify")[0].InnerText);
                _VM.IntgrScanWindowTime = bool.Parse(doc.GetElementsByTagName("IntgrScanWindowTime")[0].InnerText);
                _VM.PrefixTime = bool.Parse(doc.GetElementsByTagName("PrefixTime")[0].InnerText);
                _VM.OnlyShowTimeVR = bool.Parse(doc.GetElementsByTagName("OnlyShowTimeVR")[0].InnerText);
                _VM.Time24H = bool.Parse(doc.GetElementsByTagName("Time24H")[0].InnerText);
                _VM.ScanInterval = int.Parse(doc.GetElementsByTagName("ScanInterval")[0].InnerText);
                _VM.CurrentMenuItem = int.Parse(doc.GetElementsByTagName("CurrentMenuItem")[0].InnerText);
                _VM.OSCIP = doc.GetElementsByTagName("OSCIP")[0].InnerText;
                _VM.OSCPortOut = int.Parse(doc.GetElementsByTagName("OSCPortOut")[0].InnerText);
                _VM.PrefixIconMusic = bool.Parse(doc.GetElementsByTagName("PrefixIconMusic")[0].InnerText);
                _VM.PrefixIconStatus = bool.Parse(doc.GetElementsByTagName("PrefixIconStatus")[0].InnerText);
                _VM.ScanPauseTimeout = int.Parse(doc.GetElementsByTagName("ScanPauseTimeout")[0].InnerText);
                _VM.PrefixChat = bool.Parse(doc.GetElementsByTagName("PrefixChat")[0].InnerText);
                _VM.ChatFX = bool.Parse(doc.GetElementsByTagName("ChatFX")[0].InnerText);
                _VM.PauseIconMusic = bool.Parse(doc.GetElementsByTagName("PauseIconMusic")[0].InnerText);
                _VM.Topmost = bool.Parse(doc.GetElementsByTagName("Topmost")[0].InnerText);
                _VM.RecentTikTokTTSVoice = doc.GetElementsByTagName("RecentTikTokTTSVoice")[0].InnerText;
                _VM.TTSTikTokEnabled = bool.Parse(doc.GetElementsByTagName("TTSTikTokEnabled")[0].InnerText);
                _VM.TTSCutOff = bool.Parse(doc.GetElementsByTagName("TTSCutOff")[0].InnerText);
                _VM.RecentPlayBackOutput = doc.GetElementsByTagName("RecentPlayBackOutput")[0].InnerText;

            }
            catch (Exception ex)
            {

            }
        }





        public void LoadStatusList()
        {
            if (File.Exists(Path.Combine(_VM.DataPath, "StatusList.xml")))
            {
                string json = File.ReadAllText(Path.Combine(_VM.DataPath, "StatusList.xml"));
                _VM.StatusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
            }
            else
            {
                Random random = new Random();
                int randomId = random.Next(10, 99999999);
                _VM.StatusList.Add(new StatusItem { CreationDate = DateTime.Now, IsActive = true, IsFavorite = true, msg = "Bubs", MSGLenght = 4, MSGID = randomId });
                _VM.StatusList.Add(new StatusItem { CreationDate = DateTime.Now, IsActive = false, IsFavorite = true, msg = "Enjoy <$", MSGLenght = 8, MSGID = randomId });
                _VM.SaveStatusList();
            }
        }

        public void CheckForUpdate()
        {
            try
            {
                string token = _ENCRY.DecryptString(_VM.ApiStream);
                string url = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases/latest";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                    client.DefaultRequestHeaders.Add("User-Agent", "vrcosc-magicchatbox-update-checker");
                    var response = client.GetAsync(url).Result;
                    var json = response.Content.ReadAsStringAsync().Result;
                    dynamic release = JsonConvert.DeserializeObject(json);
                    string latestVersion = release.tag_name;
                    _VM.GitHubVersion = new Version(Regex.Replace(latestVersion, "[^0-9.]", ""));
                    if (_VM.GitHubVersion != null)
                    {
                        CompareVersions();
                    }
                }

            }
            catch (Exception)
            {

                _VM.VersionTxt = "Can't check updates";
                _VM.VersionTxtColor = "#F36734";
            }

        }

        public void CompareVersions()
        {

            try
            {
                var currentVersion = _VM.AppVersion.VersionNumber;
                var githubVersion = _VM.GitHubVersion.VersionNumber;
                int result = currentVersion.CompareTo(githubVersion);
                if (result < 0)
                {
                    _VM.VersionTxt = "New version available";
                    _VM.VersionTxtColor = "#FF8AFF04";
                }
                else if (result == 0)
                {
                    _VM.VersionTxt = "You are up-to-date";
                    _VM.VersionTxtColor = "#FF92CC90";
                }
                else
                {
                    _VM.VersionTxt = "You running a preview, fun!";
                    _VM.VersionTxtColor = "#FFE816EA";
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
