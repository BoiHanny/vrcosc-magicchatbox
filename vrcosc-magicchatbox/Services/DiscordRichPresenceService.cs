using DiscordRPC;
using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services;

public sealed class DiscordRichPresenceService : IDisposable
{
    private readonly DiscordSettings _settings;
    private readonly IAppState _appState;
    private readonly Lazy<IModuleHost> _modules;
    private readonly object _sync = new();

    private DiscordRpcClient? _client;
    private string? _clientId;
    private string? _lastInvalidClientId;
    private string? _lastSignature;
    private PresenceSnapshot _lastSnapshot = PresenceSnapshot.Empty;

    public bool IsRunning => _client?.IsInitialized == true;

    public DiscordRichPresenceService(
        ISettingsProvider<DiscordSettings> settingsProvider,
        IAppState appState,
        Lazy<IModuleHost> modules)
    {
        _settings = settingsProvider.Value;
        _appState = appState;
        _modules = modules;
    }

    public Task UpdateAsync(
        string? worldName,
        int playerCount,
        string? instanceType,
        string? region,
        string? joinUrl,
        DateTimeOffset? worldJoinedAt)
    {
        _lastSnapshot = new PresenceSnapshot(worldName, playerCount, instanceType, region, joinUrl, worldJoinedAt);

        if (!_settings.EnableRichPresence)
            return ClearAsync();

        if (!EnsureClient())
            return Task.CompletedTask;

        var details = ReplacePlaceholders(_settings.RichPresenceDetails, _lastSnapshot);
        var state = ReplacePlaceholders(_settings.RichPresenceState, _lastSnapshot);
        string clientId = ResolveClientId();
        string signature = string.Join('\u001f',
            clientId,
            details,
            state,
            _settings.RichPresenceLargeText,
            _settings.RichPresenceLargeImageKey,
            _settings.RichPresenceSmallImageKey,
            _settings.RichPresenceSmallText,
            _settings.RichPresenceShowElapsed.ToString(),
            worldJoinedAt?.ToUnixTimeSeconds().ToString() ?? string.Empty,
            _settings.RichPresenceShowJoinButton.ToString(),
            _settings.RichPresenceJoinButtonLabel,
            joinUrl ?? string.Empty);

        if (signature == _lastSignature)
            return Task.CompletedTask;

        try
        {
            var presence = new RichPresence
            {
                Details = TruncatePresenceField(details, 128),
                State = TruncatePresenceField(state, 128),
                Assets = new Assets
                {
                    LargeImageKey = EmptyToNull(_settings.RichPresenceLargeImageKey),
                    LargeImageText = TruncatePresenceField(_settings.RichPresenceLargeText, 128),
                    SmallImageKey = EmptyToNull(_settings.RichPresenceSmallImageKey),
                    SmallImageText = TruncatePresenceField(_settings.RichPresenceSmallText, 128)
                }
            };

            if (_settings.RichPresenceShowElapsed && worldJoinedAt.HasValue)
                presence.Timestamps = new Timestamps(worldJoinedAt.Value.UtcDateTime);

            if (_settings.RichPresenceShowJoinButton
                && IsPublicInstance(instanceType)
                && IsHttpsUrl(joinUrl))
            {
                string joinButtonLabel = string.IsNullOrWhiteSpace(_settings.RichPresenceJoinButtonLabel)
                    ? "Join World"
                    : _settings.RichPresenceJoinButtonLabel.Trim();

                presence.Buttons =
                [
                    new Button
                    {
                        Label = TruncatePresenceField(joinButtonLabel, 32),
                        Url = joinUrl!
                    }
                ];
            }

            _client!.SetPresence(presence);
            _lastSignature = signature;
            Logging.WriteInfo($"Discord Rich Presence: updated via DiscordRichPresence package (clientId={MaskClientId(clientId)}, world='{worldName ?? "none"}', players={playerCount}).");
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        return Task.CompletedTask;
    }

    public Task RefreshLastAsync()
        => UpdateAsync(
            _lastSnapshot.WorldName,
            _lastSnapshot.PlayerCount,
            _lastSnapshot.InstanceType,
            _lastSnapshot.Region,
            _lastSnapshot.JoinUrl,
            _lastSnapshot.WorldJoinedAt);

    public Task ClearAsync()
    {
        lock (_sync)
        {
            if (_client?.IsInitialized == true)
            {
                try
                {
                    _client.ClearPresence();
                    Logging.WriteInfo("Discord Rich Presence: cleared.");
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, MSGBox: false);
                }
            }

            _lastSignature = null;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_client == null)
                return;

            try
            {
                if (_client.IsInitialized)
                    _client.ClearPresence();
                _client.Dispose();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                _client = null;
                _clientId = null;
                _lastSignature = null;
            }
        }
    }

    private bool EnsureClient()
    {
        lock (_sync)
        {
            string clientId = ResolveClientId();
            if (_client?.IsInitialized == true && string.Equals(_clientId, clientId, StringComparison.Ordinal))
                return true;

            try
            {
                if (_client != null)
                {
                    try
                    {
                        if (_client.IsInitialized)
                            _client.ClearPresence();
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteException(ex, MSGBox: false);
                    }

                    _client.Dispose();
                }

                _client = new DiscordRpcClient(clientId, autoEvents: true);
                AttachEventLogging(_client, clientId);
                _clientId = clientId;
                bool initialized = _client.Initialize();
                Logging.WriteInfo($"Discord Rich Presence: DiscordRichPresence client initialize result={initialized} (clientId={MaskClientId(clientId)}, autoEvents=true).");
                return initialized;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                _client = null;
                _clientId = null;
                return false;
            }
        }
    }

    private static void AttachEventLogging(DiscordRpcClient client, string clientId)
    {
        client.OnConnectionEstablished += (_, _) =>
            Logging.WriteInfo($"Discord Rich Presence: connected to local Discord client (clientId={MaskClientId(clientId)}).");
        client.OnReady += (_, _) =>
            Logging.WriteInfo($"Discord Rich Presence: ready to publish activity (clientId={MaskClientId(clientId)}).");
        client.OnConnectionFailed += (_, _) =>
            Logging.WriteInfo($"Discord Rich Presence: failed to connect to local Discord client (clientId={MaskClientId(clientId)}). Is Discord running?");
        client.OnError += (_, _) =>
            Logging.WriteInfo($"Discord Rich Presence: Discord reported an activity payload error (clientId={MaskClientId(clientId)}).");
        client.OnClose += (_, _) =>
            Logging.WriteInfo($"Discord Rich Presence: local Discord connection closed (clientId={MaskClientId(clientId)}).");
        client.OnPresenceUpdate += (_, _) =>
            Logging.WriteDebug($"Discord Rich Presence: Discord accepted presence update (clientId={MaskClientId(clientId)}).");
    }

    private static string MaskClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return "empty";

        string trimmed = clientId.Trim();
        return trimmed.Length <= 6
            ? "***"
            : $"{trimmed[..3]}***{trimmed[^3..]}";
    }

    private string ReplacePlaceholders(string template, PresenceSnapshot snapshot)
    {
        string media = BuildMediaText();
        string mode = _appState.IsVRRunning ? "VR" : "Desktop";
        string status = _settings.RichPresenceShowVrDesktopMode
            ? $"{mode} mode"
            : "MagicChatbox";
        string world = string.IsNullOrWhiteSpace(snapshot.WorldName)
            ? "Not in a world"
            : snapshot.WorldName;
        string instanceType = string.IsNullOrWhiteSpace(snapshot.InstanceType)
            ? "Idle"
            : snapshot.InstanceType;
        string region = string.IsNullOrWhiteSpace(snapshot.Region)
            ? "Local"
            : snapshot.Region;

        return (template ?? string.Empty)
            .Replace("{world}", world)
            .Replace("{count}", snapshot.PlayerCount.ToString())
            .Replace("{type}", instanceType)
            .Replace("{region}", region)
            .Replace("{status}", status)
            .Replace("{mode}", mode)
            .Replace("{time}", DateTime.Now.ToShortTimeString())
            .Replace("{media}", media)
            .Replace("{unique}", GetVrcRadarValue(m => m.UniquePlayersCount).ToString())
            .Replace("{peak}", GetVrcRadarValue(m => m.PeakPlayerCountThisSession).ToString())
            .Replace("{worlds}", GetVrcRadarValue(m => m.WorldsVisited).ToString());
    }

    private string BuildMediaText()
    {
        try
        {
            var spotify = _modules.Value.Spotify;
            if (spotify?.Display.HasPlayback == true && !string.IsNullOrWhiteSpace(spotify.Display.Title))
            {
                return string.IsNullOrWhiteSpace(spotify.Display.Artist)
                    ? spotify.Display.Title
                    : $"{spotify.Display.Title} - {spotify.Display.Artist}";
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        return "MagicChatbox";
    }

    private int GetVrcRadarValue(Func<VrcLogModule, int> selector)
    {
        try
        {
            var radar = _modules.Value.VrcRadar;
            return radar == null ? 0 : selector(radar);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return 0;
        }
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private string ResolveClientId()
    {
        if (DiscordOAuthHandler.TryNormalizeClientId(_settings.VoiceClientId, out string clientId, out _))
        {
            _lastInvalidClientId = null;
            return clientId;
        }

        if (!string.Equals(_lastInvalidClientId, _settings.VoiceClientId, StringComparison.Ordinal))
        {
            _lastInvalidClientId = _settings.VoiceClientId;
            Logging.WriteInfo("Discord Rich Presence: invalid Application ID, using bundled MagicChatbox Application ID.");
        }

        return Constants.DiscordClientId;
    }

    private static string TruncatePresenceField(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }

    private static bool IsPublicInstance(string? instanceType)
        => string.Equals(instanceType, "Public", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpsUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps;

    private sealed record PresenceSnapshot(
        string? WorldName,
        int PlayerCount,
        string? InstanceType,
        string? Region,
        string? JoinUrl,
        DateTimeOffset? WorldJoinedAt)
    {
        public static PresenceSnapshot Empty { get; } = new(null, 0, null, null, null, null);
    }
}
