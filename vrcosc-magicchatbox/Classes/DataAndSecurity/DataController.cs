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
using System.Windows;
using System.Xml;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;
using Version = vrcosc_magicchatbox.ViewModels.Models.Version;

namespace vrcosc_magicchatbox.DataAndSecurity
{
    public static class DataController
    {
        private static bool isUpdateCheckRunning = false;

        public static NetworkStatisticsModule networkStatisticsModule = null;



        private static readonly Dictionary<char, string> SuperscriptMapping = new Dictionary<char, string>
        {
            {'/', "·"}, {':', "'"}, {'a', "ᵃ"}, {'b', "ᵇ"}, {'c', "ᶜ"}, {'d', "ᵈ"}, {'e', "ᵉ"},
            {'f', "ᶠ"}, {'g', "ᵍ"}, {'h', "ʰ"}, {'i', "ⁱ"}, {'j', "ʲ"},
            {'k', "ᵏ"}, {'l', "ˡ"}, {'m', "ᵐ"}, {'n', "ⁿ"}, {'o', "ᵒ"},
            {'p', "ᵖ"}, {'q', "ᵒ"}, {'r', "ʳ"}, {'s', "ˢ"}, {'t', "ᵗ"},
            {'u', "ᵘ"}, {'v', "ᵛ"}, {'w', "ʷ"}, {'x', "ˣ"}, {'y', "ʸ"},
            {'z', "ᶻ"}, {'0', "⁰"}, {'1', "¹"}, {'2', "²"}, {'3', "³"},
            {'4', "⁴"}, {'5', "⁵"}, {'6', "⁶"}, {'7', "⁷"}, {'8', "⁸"},
            {'9', "⁹"}, {',', "'"}, {'.', "'"} , {'%', "⁒"}
        };


        public static string GetApplicationVersion()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                AssemblyName assemblyName = assembly.GetName();
                string versionString = assemblyName.Version.ToString(); // For example, "0.9.1.0"

                // Use the Version class to enforce the correct format
                var version = new Version(versionString);

                return version.VersionNumber;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return "69.420.666"; // Fallback in case of error
            }
        }


        public static string TransformToSuperscript(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return new string(input.ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '/' || c == ':' || c == ',' || c == '.' || c == '%')
                .Select(c => char.IsWhiteSpace(c) ? " " : (SuperscriptMapping.ContainsKey(c) ? SuperscriptMapping[c] : c.ToString()))
                .SelectMany(s => s)
                .ToArray());
        }

        private static async void CheckForUpdate()
        {
            try
            {
                string urlLatest = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases/latest";
                string urlPreRelease = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases";

                bool isWithinRateLimit = await CheckRateLimit();

                using (var client = new HttpClient())
                {
                    if (!isWithinRateLimit && !string.IsNullOrEmpty(ViewModel.Instance.ApiStream))
                    {
                        string token = EncryptionMethods.DecryptString(ViewModel.Instance.ApiStream);
                        client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                    }

                    client.DefaultRequestHeaders.Add("User-Agent", "vrcosc-magicchatbox-update-checker");

                    // Check the latest release
                    HttpResponseMessage responseLatest = client.GetAsync(urlLatest).Result;
                    var jsonLatest = responseLatest.Content.ReadAsStringAsync().Result;
                    JObject releaseLatest = JObject.Parse(jsonLatest);
                    string latestVersion = releaseLatest.Value<string>("tag_name");

                    ViewModel.Instance.LatestReleaseVersion = new Version(
                        Regex.Replace(latestVersion, "[^0-9.]", string.Empty));

                    // Correctly handling the assets array to get the browser_download_url
                    JArray assetsLatest = releaseLatest.Value<JArray>("assets");
                    if (assetsLatest != null && assetsLatest.Count > 0)
                    {
                        string downloadUrl = assetsLatest[0].Value<string>("browser_download_url");
                        ViewModel.Instance.LatestReleaseURL = downloadUrl; // Store the download URL
                    }

                    // Check the latest pre-release
                    var responsePreRelease = client.GetAsync(urlPreRelease).Result;
                    var jsonPreRelease = responsePreRelease.Content.ReadAsStringAsync().Result;
                    JArray releases = JArray.Parse(jsonPreRelease);
                    string preReleaseVersion = string.Empty;
                    foreach (var release in releases)
                    {
                        if (release.Value<bool>("prerelease"))
                        {
                            preReleaseVersion = release.Value<string>("tag_name");
                            JArray assetsPreRelease = release.Value<JArray>("assets");
                            if (assetsPreRelease != null && assetsPreRelease.Count > 0)
                            {
                                string preReleaseDownloadUrl = assetsPreRelease[0].Value<string>("browser_download_url");
                                ViewModel.Instance.PreReleaseURL = preReleaseDownloadUrl; // Store the download URL
                            }
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

                    UpdateApp updater = new UpdateApp();
                    ViewModel.Instance.RollBackUpdateAvailable = updater.CheckIfBackupExists();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                ViewModel.Instance.VersionTxt = "Can't check updates";
                ViewModel.Instance.VersionTxtColor = "#F36734";
                ViewModel.Instance.VersionTxtUnderLine = false;
            }
            finally
            {
                CompareVersions();
            }
        }

        private static async Task<bool> CheckRateLimit()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "vrcosc-magicchatbox-update-checker");

                    // Check the rate limit status
                    var rateLimitResponse = await client.GetAsync("https://api.github.com/rate_limit");
                    var rateLimitData = JsonConvert.DeserializeObject<JObject>(await rateLimitResponse.Content.ReadAsStringAsync());

                    // Check if the rate limit has been exceeded for the requested endpoint
                    var resources = rateLimitData["resources"];
                    var coreResource = resources["core"];
                    var remainingRequests = (int)coreResource["remaining"];

                    if (remainingRequests <= 0)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }

            return true;
        }

        private static object ConvertToType(Type targetType, string value)
        {
            if (targetType == typeof(bool))
            {
                if (bool.TryParse(value, out bool boolResult))
                    return boolResult;
                return false; // Default for bool
            }
            if (targetType == typeof(int))
            {
                if (int.TryParse(value, out int intResult))
                    return intResult;
                return 0; // Default for int
            }
            if (targetType == typeof(string))
            {
                return value;
            }
            if (targetType == typeof(float))
            {
                if (float.TryParse(value, out float floatResult))
                    return floatResult;
                return 0f; // Default for float
            }
            if (targetType == typeof(double))
            {
                if (double.TryParse(value, out double doubleResult))
                    return doubleResult;
                return 0.0; // Default for double
            }
            if (targetType == typeof(Timezone))
            {
                if (Enum.TryParse(typeof(Timezone), value, out var timezoneResult))
                    return timezoneResult;
                return Timezone.UTC; // Default timezone
            }
            if (targetType == typeof(MediaLinkTimeSeekbar))
            {
                if (Enum.TryParse(typeof(MediaLinkTimeSeekbar), value, out var seekbarResult))
                    return seekbarResult;
                return MediaLinkTimeSeekbar.SmallNumbers; // Default enum value
            }
            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out DateTime dateTimeResult))
                    return dateTimeResult;
                return DateTime.MinValue; // Default for DateTime
            }
            // Handle ObservableCollection<string>
            if (targetType == typeof(ObservableCollection<string>))
            {
                try
                {
                    // Assuming the value is a JSON array
                    var list = JsonConvert.DeserializeObject<List<string>>(value);
                    return new ObservableCollection<string>(list ?? new List<string>());
                }
                catch (Exception)
                {
                    // Fallback to empty collection if parsing fails
                    return new ObservableCollection<string>();
                }
            }
            throw new NotSupportedException($"Unsupported type: {targetType}");
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
        { "IntgrNetworkStatistics", (typeof(bool), "Integrations") },
        { "IntgrScanMediaLink", (typeof(bool), "Integrations") },
        { "IntgrComponentStats", (typeof(bool), "Integrations") },
        { "IntgrSoundpad", (typeof(bool), "Integrations") },

        { "IntgrComponentStats_VR", (typeof(bool), "IntegrationToggles") },
        { "IntgrComponentStats_DESKTOP", (typeof(bool), "IntegrationToggles") },

        { "IntgrNetworkStatistics_VR", (typeof(bool), "IntegrationToggles") },
        { "IntgrNetworkStatistics_DESKTOP", (typeof(bool), "IntegrationToggles") },

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

        { "IntgrSoundpad_DESKTOP", (typeof(bool), "IntegrationToggles") },
        { "IntgrSoundpad_VR", (typeof(bool), "IntegrationToggles") },

        { "Time24H", (typeof(bool), "Time") },
        { "PrefixTime", (typeof(bool), "Time") },
        { "TimeShowTimeZone", (typeof(bool), "Time") },
        { "SelectedTimeZone", (typeof(Timezone), "Time") },
        { "UseDaylightSavingTime", (typeof(bool), "Time") },
        { "AutoSetDaylight", (typeof(bool), "Time") },
        { "UseSystemCulture", (typeof(bool), "Time") },

        { "CurrentMenuItem", (typeof(int), "Menu") },

        { "NetworkStats_ShowCurrentDown", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowCurrentUp", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowMaxDown", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowMaxUp", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowTotalDown", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowTotalUp", (typeof(bool), "NetworkStatistics") },
        { "NetworkStats_ShowNetworkUtilization", (typeof(bool), "NetworkStatistics") },

        { "OpenAIAccessTokenEncrypted", (typeof(string), "OpenAI") },
        { "OpenAIOrganizationIDEncrypted", (typeof(string), "OpenAI") },

        { "SelectedGPU", (typeof(string), "ComponentStats") },
        { "AutoSelectGPU", (typeof(bool), "ComponentStats") },
        { "UseEmojisForTempAndPower", (typeof(bool), "ComponentStats") },
        { "IsTemperatureSwitchEnabled", (typeof(bool), "ComponentStats") },

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
        { "MediaLinkTimeSeekStyle", (typeof(MediaLinkTimeSeekbar), "MediaLink") },
        { "AutoDowngradeSeekbar", (typeof(bool), "MediaLink") },

        { "ScanningInterval", (typeof(double), "Scanning") },
        { "ScanPauseTimeout", (typeof(int), "Scanning") },

        { "PrefixIconStatus", (typeof(bool), "Icons") },
        { "PrefixIconMusic", (typeof(bool), "Icons") },
        { "PauseIconMusic", (typeof(bool), "Icons") },
        { "PrefixIconSoundpad", (typeof(bool), "Icons") },
        { "EmojiCollection", (typeof(ObservableCollection<string>), "Icons") },
        { "EnableEmojiShuffleInChats", (typeof(bool), "Icons") },
        { "EnableEmojiShuffle", (typeof(bool), "Icons") },

        { "PrefixChat", (typeof(bool), "Chat") },
        { "ChatFX", (typeof(bool), "Chat") },
        { "ChatLiveEdit", (typeof(bool), "Chat") },
        { "KeepUpdatingChat", (typeof(bool), "Chat") },
        { "ChatSendAgainFX", (typeof(bool), "Chat") },
        { "ChatAddSmallDelay", (typeof(bool), "Chat") },
        { "ChatAddSmallDelayTIME", (typeof(double), "Chat") },
        { "ChattingUpdateRate", (typeof(double), "Chat") },
        { "RealTimeChatEdit", (typeof(bool), "Chat") },
        { "HideOpenAITools", (typeof(bool), "Chat") },

        { "SeperateWithENTERS", (typeof(bool), "Custom") },

        { "CountOculusSystemAsVR", (typeof(bool), "System") },
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
        { "ThirdOSCPort", (typeof(int), "OSC") },
        { "ThirdOSC", (typeof(bool), "OSC") },
        { "UnmuteThirdOutput", (typeof(bool), "OSC") },
        { "UnmuteSecOutput", (typeof(bool), "OSC") },
        { "UnmuteMainOutput", (typeof(bool), "OSC") },

        { "BlankEgg", (typeof(bool), "DEV") },

        { "SwitchStatusInterval", (typeof(int), "StatusSetting") },
        { "EggPrefixIconStatus", (typeof(string), "StatusSetting") },
        { "IsRandomCycling", (typeof(bool), "StatusSetting") },
        { "CycleStatus", (typeof(bool), "StatusSetting") },

        { "WindowActivityShowFocusedApp", (typeof(bool), "WindowActivity") },
        { "WindowActivityDesktopFocusTitle", (typeof(string), "WindowActivity") },
        { "WindowActivityDesktopTitle", (typeof(string), "WindowActivity") },
        { "WindowActivityVRFocusTitle", (typeof(string), "WindowActivity") },
        { "WindowActivityVRTitle", (typeof(string), "WindowActivity") },

        { "PulsoidAccessTokenOAuthEncrypted", (typeof(string), "PulsoidConnector") },
        { "PulsoidAuthConnected", (typeof(bool), "PulsoidConnector") },


        { "Settings_Status", (typeof(bool), "OptionsTabState") },
        { "Settings_OpenAI", (typeof(bool), "OptionsTabState") },
        { "Settings_HeartRate", (typeof(bool), "OptionsTabState") },
        { "Settings_Time", (typeof(bool), "OptionsTabState") },
        { "Settings_ComponentStats", (typeof(bool), "OptionsTabState") },
        { "Settings_NetworkStatistics", (typeof(bool), "OptionsTabState") },
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
            try
            {
                XmlNode settingNode = categoryNode.SelectSingleNode(setting.Key);
                if (settingNode != null && !string.IsNullOrEmpty(settingNode.InnerText))
                {
                    try
                    {
                        object value = ConvertToType(setting.Value.type, settingNode.InnerText);
                        property.SetValue(ViewModel.Instance, value);
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteException(new Exception($"Failed to convert setting '{setting.Key}' with value '{settingNode.InnerText}'", ex), MSGBox: false);
                        // Optionally set a default value
                        SetDefaultValue(property, setting.Value.type);
                    }
                }
                else
                {
                    // Setting node is missing or empty
                    if (setting.Key == "EmojiCollection")
                    {
                        // Do NOT set to default. Retain existing value in ViewModel.
                        Logging.WriteInfo($"Setting '{setting.Key}' not found or empty in XML. Retaining existing value.");
                    }
                    else
                    {
                        // For other settings, set default value
                        Logging.WriteInfo($"Setting '{setting.Key}' not found or empty in XML. Setting to default.");
                        SetDefaultValue(property, setting.Value.type);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception($"Error loading setting '{setting.Key}'", ex), MSGBox: false);
                // Continue processing other settings
            }
        }



        private static void SetDefaultValue(PropertyInfo property, Type type)
        {
            object defaultValue;
            if (type == typeof(ObservableCollection<string>))
            {
                // Initialize with an empty collection instead of null
                defaultValue = new ObservableCollection<string>();
            }
            else
            {
                defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
            }
            property.SetValue(ViewModel.Instance, defaultValue);
        }



        private static void SaveSettingToXML(
            XmlDocument xmlDoc,
            XmlNode categoryNode,
            KeyValuePair<string, (Type type, string category)> setting,
            PropertyInfo property)
        {
            try
            {
                object value = property.GetValue(ViewModel.Instance);
                if (value != null)
                {
                    string serializedValue;

                    if (value is ObservableCollection<string> collection)
                    {
                        // Serialize the collection to JSON
                        serializedValue = JsonConvert.SerializeObject(collection);
                    }
                    else
                    {
                        serializedValue = value.ToString();
                    }

                    if (!string.IsNullOrEmpty(serializedValue))
                    {
                        XmlNode settingNode = xmlDoc.CreateElement(setting.Key);
                        settingNode.InnerText = serializedValue;
                        categoryNode.AppendChild(settingNode);
                    }
                    else
                    {
                        // Handle null or empty value if necessary
                        Logging.WriteInfo($"Setting '{setting.Key}' has null or empty value. Skipping save.");
                    }
                }
                else
                {
                    // Handle null value if necessary
                    Logging.WriteInfo($"Setting '{setting.Key}' is null. Skipping save.");
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception($"Error saving setting '{setting.Key}'", ex), MSGBox: false);
                // Continue processing other settings
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
                var currentVersion = new Version(ViewModel.Instance.AppVersion.VersionNumber).VersionNumber;
                var latestReleaseVersion = new Version(ViewModel.Instance.LatestReleaseVersion.VersionNumber).VersionNumber;

                int compareWithLatestRelease = string.Compare(currentVersion, latestReleaseVersion, StringComparison.Ordinal);

                if (compareWithLatestRelease < 0)
                {
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
                    var preReleaseVersion = new Version(ViewModel.Instance.PreReleaseVersion.VersionNumber).VersionNumber;
                    int compareWithPreRelease = string.Compare(currentVersion, preReleaseVersion, StringComparison.Ordinal);

                    if (compareWithPreRelease < 0)
                    {
                        ViewModel.Instance.VersionTxt = "Try new pre-release";
                        ViewModel.Instance.VersionTxtUnderLine = true;
                        ViewModel.Instance.VersionTxtColor = "#2FD9FF";
                        ViewModel.Instance.CanUpdate = true;
                        ViewModel.Instance.CanUpdateLabel = false;
                        ViewModel.Instance.UpdateURL = ViewModel.Instance.PreReleaseURL;
                        return;
                    }
                    else if (compareWithPreRelease == 0)
                    {
                        ViewModel.Instance.VersionTxt = "Up-to-date (pre-release)";
                        ViewModel.Instance.VersionTxtUnderLine = false;
                        ViewModel.Instance.VersionTxtColor = "#75D5FE";
                        ViewModel.Instance.CanUpdateLabel = false;
                        ViewModel.Instance.CanUpdate = false;
                        return;
                    }
                }

                if (compareWithLatestRelease > 0)
                {
                    ViewModel.Instance.VersionTxt = "✨ Supporter version ✨";
                    ViewModel.Instance.VersionTxtColor = "#FFD700";
                    ViewModel.Instance.VersionTxtUnderLine = false;
                    ViewModel.Instance.CanUpdate = false;
                    ViewModel.Instance.CanUpdateLabel = false;
                    return;
                }

                ViewModel.Instance.VersionTxt = "You are up-to-date";
                ViewModel.Instance.VersionTxtUnderLine = false;
                ViewModel.Instance.VersionTxtColor = "#FF92CC90";
                ViewModel.Instance.CanUpdateLabel = false;
                ViewModel.Instance.CanUpdate = false;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
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
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }
        }

        public static void LoadAppList()
        {
            try
            {
                if (ViewModel.Instance == null)
                {
                    Logging.WriteInfo("ViewModel is null, not a problem :P");
                    return;
                }

                string appHistoryPath = Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml");

                if (File.Exists(appHistoryPath))
                {
                    string json = File.ReadAllText(appHistoryPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var scannedApps = JsonConvert.DeserializeObject<ObservableCollection<ProcessInfo>>(json);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ViewModel.Instance.ScannedApps = scannedApps ?? new();
                        });
                    }
                }
                else
                {
                    Logging.WriteInfo("AppHistory history has never been created, not a problem :P");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ViewModel.Instance.ScannedApps = new();
                    });
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.ScannedApps = new();
                });
            }
        }


        public static void LoadChatList()
        {
            try
            {
                if (ViewModel.Instance == null)
                {
                    Logging.WriteInfo("ViewModel is null, not a problem :P");
                    return;
                }

                if (File.Exists(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml")))
                {
                    string json = File.ReadAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml"));
                    if (json.ToLower().Equals("null"))
                    {
                        Logging.WriteInfo("LastMessages history is null, not problem :P");
                        ViewModel.Instance.LastMessages = new();
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
                    if (ViewModel.Instance.LastMessages == null)
                    {
                        ViewModel.Instance.LastMessages = new();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                if (ViewModel.Instance.ScannedApps == null)
                {
                    ViewModel.Instance.ScannedApps = new();
                }
            }
        }


        public static void LoadMediaSessions()
        {
            try
            {
                if (ViewModel.Instance == null)
                {
                    Logging.WriteInfo("ViewModel is null, not a problem :P");
                    return;
                }

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
                    if (ViewModel.Instance.SavedSessionSettings == null)
                    {
                        ViewModel.Instance.SavedSessionSettings = new List<MediaSessionSettings>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
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
                if (ViewModel.Instance == null)
                {
                    Logging.WriteInfo("ViewModel is null.");
                    return;
                }

                string statusListPath = Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml");
                if (File.Exists(statusListPath))
                {
                    string json = File.ReadAllText(statusListPath);
                    UpdateStatusListFromJson(json);
                }
                else
                {
                    InitializeStatusListWithDefaults();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                EnsureStatusListInitialized();
            }
        }



        private static void UpdateStatusListFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                Logging.WriteInfo("StatusList history is empty or null.");
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>();
                return;
            }

            try
            {
                var statusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
                if (statusList != null)
                {
                    ViewModel.Instance.StatusList = statusList;
                    CheckForSpecialMessages(statusList);
                }
            }
            catch (JsonException jsonEx)
            {
                Logging.WriteException(jsonEx, MSGBox: true);
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>();
            }
        }

        private static void CheckForSpecialMessages(ObservableCollection<StatusItem> statusList)
        {
            if (statusList.Any(x => x.msg.Equals("boihanny", StringComparison.OrdinalIgnoreCase) ||
                                    x.msg.Equals("sr4 series", StringComparison.OrdinalIgnoreCase)))
            {
                ViewModel.Instance.Egg_Dev = true;
            }
            if (statusList.Any(x => x.msg.Equals("izurubae", StringComparison.OrdinalIgnoreCase)))
            {
                ViewModel.Instance.IzuruBaeMode = true;
            }
        }

        private static void InitializeStatusListWithDefaults()
        {
            ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>
    {
        new StatusItem { CreationDate = DateTime.Now, IsActive = true, msg = "Enjoy 💖", MSGID = GenerateRandomId() },
        new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Below you can create your own status", MSGID = GenerateRandomId() },
        new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Activate it by clicking the power icon", MSGID = GenerateRandomId() }
    };
            ViewModel.SaveStatusList();
        }

        private static void EnsureStatusListInitialized()
        {
            if (ViewModel.Instance.StatusList == null)
            {
                ViewModel.Instance.StatusList = new ObservableCollection<StatusItem>();
            }
        }

        private static int GenerateRandomId()
        {
            Random random = new Random();
            return random.Next(10, 99999999);
        }

        public static void EnsureLogDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }


        public static void ManageSettingsXML(bool saveSettings = false)
        {
            try
            {
                if (ViewModel.Instance == null)
                {
                    Logging.WriteException(new Exception("ViewModel is null, please restart the program."), exitapp: true);
                }

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
                        Logging.WriteInfo($"Settings file '{datapath}' does not exist. Using defaults.");
                        return;
                    }

                    xmlDoc.Load(datapath);
                    rootNode = xmlDoc.SelectSingleNode("Settings");
                    if (rootNode == null)
                    {
                        Logging.WriteInfo("Settings root node missing in XML. Using defaults.");
                        return;
                    }
                }

                var settings = InitializeSettingsDictionary();

                foreach (var setting in settings)
                {
                    try
                    {
                        PropertyInfo property = ViewModel.Instance.GetType().GetProperty(setting.Key);
                        if (property == null)
                        {
                            Logging.WriteInfo($"Property '{setting.Key}' not found in ViewModel. Skipping.");
                            continue;
                        }

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
                        Logging.WriteException(new Exception($"Error processing setting '{setting.Key}'", ex), MSGBox: false);
                        // Continue with other settings
                    }
                }

                if (saveSettings)
                {
                    xmlDoc.Save(datapath);
                    Logging.WriteInfo("All settings have been saved successfully.");
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception("Error managing settings XML.", ex), MSGBox: false);
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
                Logging.WriteException(ex, MSGBox: false);
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
                Logging.WriteException(ex, MSGBox: false);
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

                    if (string.IsNullOrEmpty(json))
                    {
                        return;
                    }

                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "AppHistory.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public static void SaveChatList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {

                    if (ViewModel.Instance.LastMessages == null)
                    {
                        return;
                    }

                    if (ViewModel.Instance.LastMessages.Count == 0)
                    {
                        return;
                    }

                    string json = JsonConvert.SerializeObject(ViewModel.Instance.LastMessages);

                    if (string.IsNullOrEmpty(json))
                    {
                        return;
                    }

                    string filePath = Path.Combine(ViewModel.Instance.DataPath, "LastMessages.xml");

                    if (string.IsNullOrEmpty(filePath))
                    {
                        return;
                    }

                    File.WriteAllText(filePath, json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private const string MediaLinkStylesFileName = "MediaLinkStyles.json";

        private static string GetMediaLinkStylesFilePath()
        {
            return Path.Combine(ViewModel.Instance.DataPath, MediaLinkStylesFileName);
        }


        public static void LoadAndSaveMediaLinkStyles(bool save = false)
        {
            try
            {
                if (save)
                {
                    SaveMediaLinkStyles();
                }
                else
                {
                    LoadMediaLinkStyles();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public static void AddNewSeekbarStyle()
        {
            // Extract custom styles (those that are not system defaults)
            ObservableCollection<MediaLinkStyle> customStyles = new ObservableCollection<MediaLinkStyle>(
                ViewModel.Instance.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault));

            // Find the highest ID among custom styles
            int highestID = customStyles.Any() ? customStyles.Max(s => s.ID) : 99;

            // Generate the next available ID starting from 100
            int nextAvailableID = highestID + 1;

            // Ensure the next ID is at least 100
            if (nextAvailableID < 100)
            {
                nextAvailableID = 100;
            }

            // Create a new style with the next available ID
            MediaLinkStyle newStyle = new MediaLinkStyle
            {
                ID = nextAvailableID,
                ProgressBarLength = 8,
                SystemDefault = false
            };

            // Add the new style to the collection
            ViewModel.Instance.MediaLinkSeekbarStyles.Add(newStyle);

            // Select the new style
            ViewModel.Instance.SelectedMediaLinkSeekbarStyle = newStyle;

            // Optionally save the updated styles
            SaveMediaLinkStyles();

            Logging.WriteInfo($"New media link style with ID {nextAvailableID} added.");
        }

        public static void DeleteSelectedSeekbarStyleAndSelectDefault()
        {
            if (ViewModel.Instance.SelectedMediaLinkSeekbarStyle == null)
            {
                return;
            }

            if (ViewModel.Instance.SelectedMediaLinkSeekbarStyle.SystemDefault)
            {
                Logging.WriteInfo("Cannot delete system default media link style.");
                return;
            }

            // Remove the selected style
            ViewModel.Instance.MediaLinkSeekbarStyles.Remove(ViewModel.Instance.SelectedMediaLinkSeekbarStyle);

            // Select the first style as the default
            ViewModel.Instance.SelectedMediaLinkSeekbarStyle = ViewModel.Instance.MediaLinkSeekbarStyles.FirstOrDefault();

            // Optionally save the updated styles
            SaveMediaLinkStyles();

            Logging.WriteInfo($"Media link style with ID {ViewModel.Instance.SelectedMediaLinkSeekbarStyle.ID} deleted.");
        }


        private static void LoadMediaLinkStyles()
        {
            // Load default styles first
            ViewModel.Instance.MediaLinkSeekbarStyles = DefaultMediaLinkStyles();
            Logging.WriteInfo("Default media link styles loaded.");

            string filePath = GetMediaLinkStylesFilePath();

            if (File.Exists(filePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(filePath);
                    var data = JsonConvert.DeserializeObject<MediaLinkStylesData>(jsonData);

                    if (data?.CustomStyles != null)
                    {
                        foreach (var style in data.CustomStyles)
                        {
                            // Avoid adding duplicates
                            if (!ViewModel.Instance.MediaLinkSeekbarStyles.Any(s => s.ID == style.ID))
                            {
                                ViewModel.Instance.MediaLinkSeekbarStyles.Add(style);
                            }
                        }
                        Logging.WriteInfo("Custom media link styles loaded.");
                    }

                    if (data?.SelectedStyleId != null)
                    {
                        var selectedStyle = ViewModel.Instance.MediaLinkSeekbarStyles.FirstOrDefault(s => s.ID == data.SelectedStyleId);
                        if (selectedStyle != null)
                        {
                            ViewModel.Instance.SelectedMediaLinkSeekbarStyle = selectedStyle;
                            Logging.WriteInfo("Selected media link style loaded.");
                        }
                        else
                        {
                            ViewModel.Instance.SelectedMediaLinkSeekbarStyle = ViewModel.Instance.MediaLinkSeekbarStyles.FirstOrDefault();
                            Logging.WriteInfo("Selected media link style not found in the loaded styles.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewModel.Instance.SelectedMediaLinkSeekbarStyle = ViewModel.Instance.MediaLinkSeekbarStyles.FirstOrDefault();
                    Logging.WriteException(ex, MSGBox: false);
                }
            }
            else
            {
                Logging.WriteInfo($"Custom media link styles file '{filePath}' not found, no problem!");
                ViewModel.Instance.SelectedMediaLinkSeekbarStyle = ViewModel.Instance.MediaLinkSeekbarStyles.FirstOrDefault();
            }
        }

        private static void SaveMediaLinkStyles()
        {
            try
            {
                // Ensure the data directory exists
                if (CreateIfMissing(ViewModel.Instance.DataPath))
                {
                    string filePath = GetMediaLinkStylesFilePath();

                    // Get all the styles that are not system default
                    ObservableCollection<MediaLinkStyle> nonSystemMediaLinkStyles = new ObservableCollection<MediaLinkStyle>(
                        ViewModel.Instance.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault));

                    var data = new MediaLinkStylesData
                    {
                        CustomStyles = nonSystemMediaLinkStyles,
                        SelectedStyleId = ViewModel.Instance.SelectedMediaLinkSeekbarStyle?.ID
                    };

                    var jsonData = JsonConvert.SerializeObject(data);
                    File.WriteAllText(filePath, jsonData);

                    Logging.WriteInfo("Custom media link styles and selected style saved.");
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private class MediaLinkStylesData
        {
            public ObservableCollection<MediaLinkStyle> CustomStyles { get; set; }
            public int? SelectedStyleId { get; set; }
        }

        public static ObservableCollection<MediaLinkStyle> DefaultMediaLinkStyles()
        {
            return new ObservableCollection<MediaLinkStyle>
            {
                new MediaLinkStyle
                {
                    ID = 1,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "▒",
                    MiddleCharacter = "▓",
                    NonFilledCharacter = "░",
                    TimePrefix = "",
                    TimeSuffix = "",
                    SystemDefault = true
                },
                new MediaLinkStyle
                {
                    ID = 2,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "▥",
                    MiddleCharacter = "▥",
                    NonFilledCharacter = "▢",
                    TimePrefix = string.Empty,
                    TimeSuffix = string.Empty,
                    SystemDefault = true
                },
                                new MediaLinkStyle
                {
                    ID = 3,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "●",
                    MiddleCharacter = "◐",
                    NonFilledCharacter = "○",
                    TimePrefix = "「",
                    TimeSuffix = "」",
                    SpaceBetweenPreSuffixAndTime = false,
                    SystemDefault = true
                },
                                new MediaLinkStyle
                {
                    ID = 4,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "♣",
                    MiddleCharacter = "♠",
                    NonFilledCharacter = "○",
                    TimePrefix = "【",
                    TimeSuffix = "】",
                    SpaceBetweenPreSuffixAndTime = false,
                    SystemDefault = true
                },
                                                            new MediaLinkStyle
            {
                    ID = 5,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "★",
                    MiddleCharacter = "✴",
                    NonFilledCharacter = "☆",
                    TimePrefix = "«",
                    TimeSuffix = "»",
                    SpaceBetweenPreSuffixAndTime = true,
                    SystemDefault = true
            },
                                                            new MediaLinkStyle
            {
                    ID = 6,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "▞",
                    MiddleCharacter = "▞",
                    NonFilledCharacter = "━",
                    TimePrefix = "┣",
                    TimeSuffix = "┫",
                    SpaceBetweenPreSuffixAndTime = false,
                    SystemDefault = true
            },
            new MediaLinkStyle
            {
                    ID = 7,
                    ProgressBarLength = 8,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "◉",
                    MiddleCharacter = "◉",
                    NonFilledCharacter = "◎",
                    TimePrefix = "",
                    TimeSuffix = "",
                    SpaceBetweenPreSuffixAndTime = false,
                    SystemDefault = true
            },
            new MediaLinkStyle
            {
                    ID = 8,
                    ProgressBarLength = 7,
                    DisplayTime = true,
                    ShowTimeInSuperscript = true,
                    FilledCharacter = "┅",
                    MiddleCharacter = "🕷️",
                    NonFilledCharacter = "┅",
                    TimePrefix = "🧙",
                    TimeSuffix = "🕸️",
                    SpaceBetweenPreSuffixAndTime = false,
                    SystemDefault = true
            },

            };
        }


        public static void SaveMediaSessions()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.SavedSessionSettings);

                    if (string.IsNullOrEmpty(json))
                    {
                        return;
                    }

                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "LastMediaLinkSessions.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }


        public static void CheckLogFolder()
        {
            try
            {
                if (CreateIfMissing(@"C:\temp\Vrcosc-MagicChatbox") == true)
                {
                    Logging.WriteInfo("Application started at: " + DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: true);
            }
        }
    }
}
