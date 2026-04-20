using System;

namespace vrcosc_magicchatbox.Core;

/// <summary>
/// Centralizes magic numbers, timeout values, and configuration constants
/// that were previously scattered throughout the codebase.
/// </summary>
public static class Constants
{
    public const int OscMaxMessageLength = 144;

    public const int HttpRetryCount = 3;
    public const int CircuitBreakerFailureThreshold = 5;
    public static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan WeatherApiTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ModerationApiTimeout = TimeSpan.FromSeconds(10);

    public const int ModuleMaxConsecutiveFailures = 3;
    public static readonly TimeSpan ModuleFaultCooldown = TimeSpan.FromSeconds(60);

    public const string TwitchApiBaseUrl = "https://api.twitch.tv/helix/";
    public const string OpenWeatherApiBaseUrl = "https://api.openweathermap.org/data/2.5/";
    public const string PulsoidApiBaseUrl = "https://dev.pulsoid.net/api/v1/";
    public const string PulsoidTokenValidateUrl = "https://dev.pulsoid.net/api/v1/token/validate";
    public const string PulsoidOAuthEndpoint = "https://pulsoid.net/oauth2/authorize";
    public const string PulsoidIntegrationsUrl = "https://pulsoid.net/ui/integrations";
    public const string TikTokTtsApiUrl = "https://gesserit.co/api/tiktok-tts";
    public const string OpenMeteoGeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";
    public const string OpenMeteoForecastUrl = "https://api.open-meteo.com/v1/forecast";
    public const string IpGeoLocationUrl = "https://ipapi.co/json/";
    public const string GitHubReleasesLatestUrl = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases/latest";
    public const string GitHubReleasesUrl = "https://api.github.com/repos/BoiHanny/vrcosc-magicchatbox/releases";
    public const string GitHubRateLimitUrl = "https://api.github.com/rate_limit";

    public const string PulsoidOAuthRedirectUri = "http://localhost:7384/";
    public const string PulsoidOAuthCallbackUri = "http://localhost:7385/";

    public const string PulsoidClientId = "1d0717d2-6c8c-47c6-9097-e289cb02a92d";
    public const string PulsoidOAuthScope = "data:heart_rate:read,profile:read,data:statistics:read";

    // Discord RPC integration
    public const string DiscordClientId = "1495716413980278814";
    public const string DiscordOAuthRedirectUri = "http://localhost:7386/";
    public const string DiscordOAuthCallbackUri = "http://localhost:7387/";
    public const string DiscordOAuthScope = "rpc rpc.voice.read";
    public const string DiscordOAuthEndpoint = "https://discord.com/oauth2/authorize";
    public const string DiscordIpcPipePrefix = "discord-ipc-";
    public const int DiscordIpcMaxPipeIndex = 9;

    public static readonly TimeSpan DiscordReconnectMinDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DiscordReconnectMaxDelay = TimeSpan.FromSeconds(30);

    public const string OpenAiOrganizationUrl = "https://platform.openai.com/account/organization";
    public const string OpenAiApiKeysUrl = "https://platform.openai.com/api-keys";
    public const string OpenAiTermsUrl = "https://openai.com/policies/terms-of-use";
    public const string OpenAiUsageUrl = "https://platform.openai.com/usage";
    public const string OpenAiOrgIdPrefix = "org-";
    public const string OpenAiApiKeyPrefix = "sk-";

    public const string TwitchDevConsoleUrl = "https://dev.twitch.tv/console/apps";
    public const string TwitchTokenGeneratorUrl = "https://twitchtokengenerator.com/";

    public const string PulsoidPricingUrl = "https://pulsoid.net/pricing?promo_campaign_id=613e3915-a6ba-40f1-a8d4-9ae68c433c6e";
    public const string DiscordInviteUrl = "https://discord.gg/ZaSFwBfhvG";
    public const string GitHubRepoUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox";
    public const string GitHubReleasesPageUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox/releases";
    public const string GitHubNewIssueUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox/issues/new/choose";
    public const string GitHubWikiBaseUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox/wiki/";
    public const string GitHubSecurityUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md";
    public const string GitHubLicenseUrl = "https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/License.md";

    // TOS version identifier — bump when terms require re-acceptance
    public const string TosVersion = "2025.03.22";

    public const string WikiMusicDisplayUrl = GitHubWikiBaseUrl + "%F0%9F%8E%BC-Music-Display";
    public const string WikiHeartRateUrl = GitHubWikiBaseUrl + "%F0%9F%A9%B5-Heart-Rate";
    public const string WikiPulsoidDiscountUrl = GitHubWikiBaseUrl + "Unlock-a-15%25-Discount-on-Pulsoid's-BRO-Plan";
    public const string WikiTtsAudioSetupUrl = GitHubWikiBaseUrl + "Play-TTS-Output-of-MagicChatbox-to-Main-Audio-Device-and-Microphone-in-VRChat-Using-VB-Audio-Cable-(Simple-Setup)";

    public static class HttpClients
    {
        public const string GitHub = "GitHub";
        public const string Pulsoid = "Pulsoid";
        public const string Weather = "Weather";
        public const string Twitch = "Twitch";
        public const string Tts = "TTS";
        public const string ModerationApi = "ModerationAPI";
    }

    public const string UpdateCheckerUserAgent = "vrcosc-magicchatbox-update-checker";

    public static readonly TimeSpan IntelliChatAutoHideDelay = TimeSpan.FromMilliseconds(2500);
    public const int DefaultMessageBoxTimeout = 10000;

    public const int MaxChatMessageLength = 141;

    public static readonly TimeSpan ManualUpdateCheckTimeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan BackgroundCheckInterval = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan AutoUpdateCheckInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan WeatherCacheExpiry = TimeSpan.FromHours(12);
    public const int UpdateSleepDelayMs = 2000;
    public const int StatusRandomIdMin = 10;
    public const int StatusRandomIdMax = 99999999;
}
