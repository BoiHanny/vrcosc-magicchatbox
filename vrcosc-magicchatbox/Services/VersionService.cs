using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Checks GitHub for application updates and compares versions.
/// Extracted from DataController — owns all update-check logic.
/// </summary>
public sealed class VersionService : IVersionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppUpdateState _updateState;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    public VersionService(
        IHttpClientFactory httpClientFactory,
        AppUpdateState updateState,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IUiDispatcher dispatcher)
    {
        _httpClientFactory = httpClientFactory;
        _updateState = updateState;
        _appSettingsProvider = appSettingsProvider;
        _dispatcher = dispatcher;
    }

    public string GetApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            string versionString = assemblyName.Version.ToString();
            var version = new ViewModels.Models.Version(versionString);
            return version.VersionNumber;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "69.420.666";
        }
    }

    public async Task CheckForUpdateAndWait(bool checkAgain = false)
    {
        _updateState.VersionTxt = "Checking for updates...";
        _updateState.VersionTxtColor = "#FBB644";
        _updateState.VersionTxtUnderLine = false;

        if (checkAgain)
            await Task.Delay(1000);

        // SemaphoreSlim prevents re-entrant update checks (fixes race in old bool guard)
        if (!await _updateLock.WaitAsync(0))
        {
            // Another check is running — wait for it to finish
            await _updateLock.WaitAsync();
            _updateLock.Release();
            return;
        }

        try
        {
            await CheckForUpdateAsync();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            const string urlLatest = Core.Constants.GitHubReleasesLatestUrl;
            const string urlPreRelease = Core.Constants.GitHubReleasesUrl;

            bool isWithinRateLimit = await CheckRateLimitAsync();
            var client = _httpClientFactory.CreateClient("GitHub");

            if (!isWithinRateLimit && !string.IsNullOrEmpty(OpenAISettings.DefaultApiStream))
            {
                string token = EncryptionMethods.DecryptString(OpenAISettings.DefaultApiStream);
                if (token != null)
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {token}");
            }

            var responseLatest = await client.GetAsync(urlLatest);
            var jsonLatest = await responseLatest.Content.ReadAsStringAsync();
            JObject releaseLatest = JObject.Parse(jsonLatest);
            string latestVersion = releaseLatest.Value<string>("tag_name");

            _updateState.LatestReleaseVersion = new ViewModels.Models.Version(
                Regex.Replace(latestVersion, "[^0-9.]", string.Empty));

            JArray assetsLatest = releaseLatest.Value<JArray>("assets");
            if (assetsLatest != null && assetsLatest.Count > 0)
            {
                _updateState.LatestReleaseURL = assetsLatest[0].Value<string>("browser_download_url");
            }

            var responsePreRelease = await client.GetAsync(urlPreRelease);
            var jsonPreRelease = await responsePreRelease.Content.ReadAsStringAsync();
            JArray releases = JArray.Parse(jsonPreRelease);

            foreach (var release in releases)
            {
                if (release.Value<bool>("prerelease"))
                {
                    string preReleaseVersion = release.Value<string>("tag_name");
                    JArray assetsPreRelease = release.Value<JArray>("assets");
                    string preReleaseDownloadUrl = null;

                    if (assetsPreRelease != null && assetsPreRelease.Count > 0)
                        preReleaseDownloadUrl = assetsPreRelease[0].Value<string>("browser_download_url");

                    if (_appSettingsProvider.Value.JoinedAlphaChannel && !string.IsNullOrEmpty(preReleaseVersion))
                    {
                        _updateState.PreReleaseVersion = new ViewModels.Models.Version(
                            Regex.Replace(preReleaseVersion, "[^0-9.]", string.Empty));
                        // Use the URL from THIS matched prerelease (fixes bug where releases[0] was used)
                        _updateState.PreReleaseURL = preReleaseDownloadUrl ?? string.Empty;
                    }
                    break;
                }
            }

            var updater = new UpdateApp(_updateState, _httpClientFactory, _dispatcher);
            _updateState.RollBackUpdateAvailable = updater.CheckIfBackupExists();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _updateState.VersionTxt = "Can't check updates";
            _updateState.VersionTxtColor = "#F36734";
            _updateState.VersionTxtUnderLine = false;
        }
        finally
        {
            CompareVersions();
        }
    }

    private async Task<bool> CheckRateLimitAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            var response = await client.GetAsync(Core.Constants.GitHubRateLimitUrl);
            var data = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());

            var remaining = (int)data["resources"]["core"]["remaining"];
            return remaining > 0;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    /// <summary>
    /// Compares current app version against latest/pre-release and updates display state.
    /// Uses numeric comparison (not string) to handle version segments correctly.
    /// </summary>
    private void CompareVersions()
    {
        try
        {
            int compareWithLatest = CompareVersionNumbers(
                _updateState.AppVersion?.VersionNumber,
                _updateState.LatestReleaseVersion?.VersionNumber);

            if (compareWithLatest < 0)
            {
                _updateState.VersionTxt = "Update now";
                _updateState.VersionTxtColor = "#FF8AFF04";
                _updateState.VersionTxtUnderLine = true;
                _updateState.CanUpdate = true;
                _updateState.CanUpdateLabel = true;
                _updateState.UpdateURL = _updateState.LatestReleaseURL;
                return;
            }

            if (_appSettingsProvider.Value.JoinedAlphaChannel && _updateState.PreReleaseVersion != null)
            {
                int compareWithPre = CompareVersionNumbers(
                    _updateState.AppVersion?.VersionNumber,
                    _updateState.PreReleaseVersion.VersionNumber);

                if (compareWithPre < 0)
                {
                    _updateState.VersionTxt = "Try new pre-release";
                    _updateState.VersionTxtUnderLine = true;
                    _updateState.VersionTxtColor = "#2FD9FF";
                    _updateState.CanUpdate = true;
                    _updateState.CanUpdateLabel = false;
                    _updateState.UpdateURL = _updateState.PreReleaseURL;
                    return;
                }
                else if (compareWithPre == 0)
                {
                    _updateState.VersionTxt = "Up-to-date (pre-release)";
                    _updateState.VersionTxtUnderLine = false;
                    _updateState.VersionTxtColor = "#75D5FE";
                    _updateState.CanUpdateLabel = false;
                    _updateState.CanUpdate = false;
                    return;
                }
            }

            if (compareWithLatest > 0)
            {
                _updateState.VersionTxt = "✨ Supporter version ✨";
                _updateState.VersionTxtColor = "#FFD700";
                _updateState.VersionTxtUnderLine = false;
                _updateState.CanUpdate = false;
                _updateState.CanUpdateLabel = false;
                return;
            }

            _updateState.VersionTxt = "You are up-to-date";
            _updateState.VersionTxtUnderLine = false;
            _updateState.VersionTxtColor = "#FF92CC90";
            _updateState.CanUpdateLabel = false;
            _updateState.CanUpdate = false;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    /// <summary>
    /// Numeric version comparison. Parses "0.9.001" into segments and compares
    /// each numerically, avoiding lexicographic bugs (e.g. "0.10" &lt; "0.9").
    /// </summary>
    private static int CompareVersionNumbers(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return string.Compare(a ?? "", b ?? "", StringComparison.Ordinal);

        var partsA = a.Split('.');
        var partsB = b.Split('.');
        int maxLen = Math.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int segA = i < partsA.Length && int.TryParse(partsA[i], out int va) ? va : 0;
            int segB = i < partsB.Length && int.TryParse(partsB[i], out int vb) ? vb : 0;
            if (segA != segB) return segA.CompareTo(segB);
        }
        return 0;
    }
}
