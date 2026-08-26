using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Updates;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

public sealed class VersionService : IVersionService
{
    private const int ReleasePageSize = 15;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppUpdateState _updateState;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAutoUpdateService _autoUpdate;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly ConcurrentDictionary<string, CachedResponse> _cache = new();

    private DateTimeOffset _quotaResetsAtUtc = DateTimeOffset.MinValue;

    public VersionService(
        IHttpClientFactory httpClientFactory,
        AppUpdateState updateState,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IUiDispatcher dispatcher,
        IAutoUpdateService autoUpdate)
    {
        _httpClientFactory = httpClientFactory;
        _updateState = updateState;
        _appSettingsProvider = appSettingsProvider;
        _dispatcher = dispatcher;
        _autoUpdate = autoUpdate;
    }

    public string GetApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            string versionString = assemblyName.Version!.ToString();
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

        if (!await _updateLock.WaitAsync(0))
        {
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
        UpdateVerdict? verdict = null;

        try
        {
            // Waiting out a limit that is already known, rather than spending a request to be
            // told about it again: GitHub answers an exhausted quota with an error, not the data.
            if (DateTimeOffset.UtcNow < _quotaResetsAtUtc)
            {
                ShowQuotaPause();
                return;
            }

            var client = _httpClientFactory.CreateClient("GitHub");

            RateLimitStatus limit = await ReadRateLimitAsync();
            if (limit.Exhausted)
            {
                _quotaResetsAtUtc = limit.ResetsAtUtc;

                if (!TryAttachFallbackToken(client))
                {
                    Logging.WriteInfo(
                        "GitHub's hourly allowance for this address is used up, so the update check is " +
                        $"held until {_quotaResetsAtUtc.ToLocalTime():HH:mm}. This is shared by everything " +
                        "on the connection, not just MagicChatbox.");
                    ShowQuotaPause();
                    return;
                }
            }

            UpdateOffer stable = UpdateOffer.Absent(UpdateChannel.Stable);
            UpdateOffer preRelease = UpdateOffer.Absent(UpdateChannel.PreRelease);

            string listUrl = $"{Core.Constants.GitHubReleasesUrl}?per_page={ReleasePageSize}";
            string listJson = await GetWithCacheAsync(client, listUrl);

            if (!string.IsNullOrWhiteSpace(listJson))
            {
                JArray releases = JArray.Parse(listJson);

                foreach (var release in releases)
                {
                    if (release.Value<bool>("draft"))
                        continue;

                    bool isPreRelease = release.Value<bool>("prerelease");

                    if (isPreRelease && preRelease.IsPresent)
                        continue;
                    if (!isPreRelease && stable.IsPresent)
                        continue;

                    UpdateOffer offer = ReadOffer(
                        release,
                        isPreRelease ? UpdateChannel.PreRelease : UpdateChannel.Stable);

                    if (!offer.IsPresent)
                        continue;

                    if (isPreRelease)
                        preRelease = offer;
                    else
                        stable = offer;

                    if (stable.IsPresent && preRelease.IsPresent)
                        break;
                }
            }

            // A repository can publish more pre-releases than fit on one page, which would leave
            // the stable channel looking empty. The dedicated endpoint always resolves it.
            if (!stable.IsPresent)
            {
                string latestJson = await GetWithCacheAsync(client, Core.Constants.GitHubReleasesLatestUrl);
                if (!string.IsNullOrWhiteSpace(latestJson))
                    stable = ReadOffer(JObject.Parse(latestJson), UpdateChannel.Stable);
            }

            PublishOffers(stable, preRelease);

            verdict = Decide(stable, preRelease);

            var updater = new UpdateApp(_updateState, _httpClientFactory, _dispatcher);
            _updateState.RollBackUpdateAvailable = updater.CheckIfBackupExists();
        }
        catch (QuotaExhaustedException ex)
        {
            // The pre-flight reading can be a little behind, and the quota is shared by everything
            // on this address, so an exhausted response can still arrive after a clean check.
            _quotaResetsAtUtc = ex.ResetsAtUtc > DateTimeOffset.UtcNow
                ? ex.ResetsAtUtc
                : DateTimeOffset.UtcNow.AddMinutes(10);

            Logging.WriteInfo(
                $"GitHub is not answering update checks from this address until {_quotaResetsAtUtc.ToLocalTime():HH:mm}.");
            ShowQuotaPause();
            return;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);

            // A check that never completed says nothing about which version is current, so the
            // failure stands rather than being painted over with a reassuring "up-to-date".
            _updateState.CanUpdate = false;
            _updateState.CanUpdateLabel = false;
            _updateState.PendingUpdateChannel = null;
            _updateState.VersionTxt = "Can't check updates";
            _updateState.VersionTxtColor = "#F36734";
            _updateState.VersionTxtUnderLine = false;
            return;
        }

        ApplyVerdict(verdict);

        if (verdict.Action == UpdateAction.AutoInstall)
            await _autoUpdate.ConsiderAsync(verdict);
    }

    private UpdateVerdict Decide(UpdateOffer stable, UpdateOffer preRelease)
    {
        AppSettings settings = _appSettingsProvider.Value;

        return UpdateDecision.Decide(
            _updateState.AppVersion?.VersionNumber,
            stable,
            preRelease,
            settings.StableUpdateMode,
            settings.PreReleaseUpdateMode,
            _autoUpdate.BlockedVersions);
    }

    private static UpdateOffer ReadOffer(JToken release, UpdateChannel channel)
    {
        string? tag = release.Value<string>("tag_name");
        if (string.IsNullOrWhiteSpace(tag))
            return UpdateOffer.Absent(channel);

        JArray? assets = release.Value<JArray>("assets");
        JToken? asset = assets?.FirstOrDefault(candidate =>
                           (candidate.Value<string>("name") ?? string.Empty)
                           .EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                       ?? assets?.FirstOrDefault();

        if (asset == null)
            return UpdateOffer.Absent(channel);

        return new UpdateOffer(
            channel,
            ReleaseVersion.Normalize(tag),
            asset.Value<string>("browser_download_url") ?? string.Empty,
            asset.Value<string>("digest") ?? string.Empty);
    }

    private void PublishOffers(UpdateOffer stable, UpdateOffer preRelease)
    {
        _updateState.LatestReleaseVersion = stable.IsPresent
            ? new ViewModels.Models.Version(stable.Version)
            : null;
        _updateState.LatestReleaseURL = stable.Url;
        _updateState.LatestReleaseDigest = stable.Digest;

        // Cleared every pass: a pre-release that has been superseded or switched off must not
        // leave a live download URL behind for a later decision to pick up.
        bool exposePreRelease = _appSettingsProvider.Value.PreReleaseUpdateMode != UpdateChannelMode.Off
                                && preRelease.IsPresent;

        _updateState.PreReleaseVersion = exposePreRelease
            ? new ViewModels.Models.Version(preRelease.Version)
            : null;
        _updateState.PreReleaseURL = exposePreRelease ? preRelease.Url : string.Empty;
        _updateState.PreReleaseDigest = exposePreRelease ? preRelease.Digest : string.Empty;
    }

    private void ApplyVerdict(UpdateVerdict verdict)
    {
        try
        {
            _updateState.PendingUpdateChannel = verdict.Standing == UpdateStanding.UpdateAvailable
                ? verdict.Channel
                : null;
            _updateState.UpdateURL = verdict.Url;
            _updateState.UpdateDigest = verdict.Digest;
            _updateState.UpdateVersion = verdict.Version;

            switch (verdict.Standing)
            {
                case UpdateStanding.UpdateAvailable when verdict.Channel == UpdateChannel.PreRelease:
                    _updateState.VersionTxt = "Try new pre-release";
                    _updateState.VersionTxtColor = "#2FD9FF";
                    _updateState.VersionTxtUnderLine = true;
                    _updateState.CanUpdate = true;
                    _updateState.CanUpdateLabel = false;
                    break;

                case UpdateStanding.UpdateAvailable:
                    _updateState.VersionTxt = "Update now";
                    _updateState.VersionTxtColor = "#FF8AFF04";
                    _updateState.VersionTxtUnderLine = true;
                    _updateState.CanUpdate = true;
                    _updateState.CanUpdateLabel = true;
                    break;

                case UpdateStanding.AheadOfReleases:
                    _updateState.VersionTxt = "✨ Supporter version ✨";
                    _updateState.VersionTxtColor = "#FFD700";
                    _updateState.VersionTxtUnderLine = false;
                    _updateState.CanUpdate = false;
                    _updateState.CanUpdateLabel = false;
                    break;

                case UpdateStanding.UpToDate when verdict.Channel == UpdateChannel.PreRelease:
                    _updateState.VersionTxt = "Up-to-date (pre-release)";
                    _updateState.VersionTxtColor = "#75D5FE";
                    _updateState.VersionTxtUnderLine = false;
                    _updateState.CanUpdate = false;
                    _updateState.CanUpdateLabel = false;
                    break;

                default:
                    _updateState.VersionTxt = "You are up-to-date";
                    _updateState.VersionTxtColor = "#FF92CC90";
                    _updateState.VersionTxtUnderLine = false;
                    _updateState.CanUpdate = false;
                    _updateState.CanUpdateLabel = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private async Task<string> GetWithCacheAsync(HttpClient client, string url)
    {
        _cache.TryGetValue(url, out CachedResponse cached);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(cached.ETag))
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

        using var response = await client.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotModified && cached.Body != null)
            return cached.Body;

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            && ReadHeader(response, "x-ratelimit-remaining") == 0)
        {
            long reset = ReadHeader(response, "x-ratelimit-reset") ?? 0;
            throw new QuotaExhaustedException(reset > 0
                ? DateTimeOffset.FromUnixTimeSeconds(reset)
                : DateTimeOffset.UtcNow.AddMinutes(10));
        }

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        string? etag = response.Headers.ETag?.ToString();

        if (!string.IsNullOrEmpty(etag))
            _cache[url] = new CachedResponse(etag, body);

        return body;
    }

    /// <summary>
    /// Asks how much of the hourly allowance is left. This endpoint is not itself counted
    /// against that allowance, so it costs nothing to ask before spending a request.
    /// </summary>
    private async Task<RateLimitStatus> ReadRateLimitAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            using var response = await client.GetAsync(Core.Constants.GitHubRateLimitUrl);
            var data = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());

            JToken? core = data?["resources"]?["core"];
            if (core == null)
                return RateLimitStatus.Unknown;

            int remaining = core.Value<int?>("remaining") ?? 1;
            long reset = core.Value<long?>("reset") ?? 0;

            return new RateLimitStatus(
                remaining <= 0,
                reset > 0 ? DateTimeOffset.FromUnixTimeSeconds(reset) : DateTimeOffset.UtcNow.AddMinutes(10));
        }
        catch (Exception ex)
        {
            // Not knowing is not the same as being out. The request itself is still worth trying,
            // and it reports its own exhaustion accurately if that turns out to be the case.
            Logging.WriteInfo($"Could not read the GitHub allowance: {ex.Message}");
            return RateLimitStatus.Unknown;
        }
    }

    private static bool TryAttachFallbackToken(HttpClient client)
    {
        if (string.IsNullOrEmpty(OpenAISettings.DefaultApiStream))
            return false;

        string? token = EncryptionMethods.DecryptString(OpenAISettings.DefaultApiStream);
        if (string.IsNullOrEmpty(token))
            return false;

        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {token}");
        return true;
    }

    private void ShowQuotaPause()
    {
        // Not an error and not a version claim: the last thing that was learned about updates
        // still stands, so nothing about the update state is disturbed here.
        _updateState.VersionTxt = $"Update check paused until {_quotaResetsAtUtc.ToLocalTime():HH:mm}";
        _updateState.VersionTxtColor = "#FBB644";
        _updateState.VersionTxtUnderLine = false;
    }

    private static long? ReadHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values)
           && long.TryParse(values.FirstOrDefault(), out long value)
            ? value
            : null;

    private readonly record struct RateLimitStatus(bool Exhausted, DateTimeOffset ResetsAtUtc)
    {
        public static readonly RateLimitStatus Unknown = new(false, DateTimeOffset.MinValue);
    }

    private sealed class QuotaExhaustedException(DateTimeOffset resetsAtUtc) : Exception("GitHub request allowance exhausted.")
    {
        public DateTimeOffset ResetsAtUtc { get; } = resetsAtUtc;
    }

    private readonly record struct CachedResponse(string ETag, string Body);
}
