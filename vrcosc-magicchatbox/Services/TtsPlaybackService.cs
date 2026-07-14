using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Handles TTS audio fetch + playback via the TikTok API.
/// Extracted from ScanLoopService to follow SRP.
/// </summary>
public sealed class TtsPlaybackService : ITtsPlaybackService
{
    private readonly Lazy<TTSModule> _tts;
    private readonly TtsAudioDisplayState _ttsAudio;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly TtsSettings _ttsSettings;
    private readonly IPrivacyConsentService _consent;
    private readonly List<CancellationTokenSource> _activeCancellationTokens = new();
    private readonly object _tokensLock = new();

    public TtsPlaybackService(
        Lazy<TTSModule> tts,
        TtsAudioDisplayState ttsAudio,
        ChatStatusDisplayState chatStatus,
        ISettingsProvider<TtsSettings> ttsSettingsProvider,
        IPrivacyConsentService consent)
    {
        _tts = tts;
        _ttsAudio = ttsAudio;
        _chatStatus = chatStatus;
        _ttsSettings = ttsSettingsProvider.Value;
        _consent = consent;
    }

    public void CancelAllTts()
    {
        CancellationTokenSource[] cancelled;
        lock (_tokensLock)
        {
            cancelled = _activeCancellationTokens.ToArray();
            _activeCancellationTokens.Clear();
        }
        foreach (var cts in cancelled)
        {
            // Only cancel here; the owning PlayTtsAsync disposes its own CTS in finally.
            // Disposing it here would make the owner's later cts2.Token access throw
            // ObjectDisposedException, misreported as a TTS error instead of a clean cancel.
            cts.Cancel();
        }
    }

    public async Task PlayTtsAsync(string chat, bool resent = false)
    {
        if (!_consent.IsApproved(PrivacyHook.InternetAccess))
        {
            _chatStatus.ChatFeedbackTxt = "TTS requires Internet Access permission";
            return;
        }

        try
        {
            if (_ttsSettings.TtsCutOff)
                CancelAllTts();

            // Registered before the fetch so TtsCutOff/CancelAllTts can cancel a TTS
            // that is still downloading, not just one that is already playing.
            var cts2 = new CancellationTokenSource();
            // Capture the token before registering: a CancellationToken stays valid after its
            // source is cancelled+disposed, so a concurrent cancel produces a clean cancel.
            CancellationToken token = cts2.Token;
            lock (_tokensLock)
                _activeCancellationTokens.Add(cts2);
            try
            {
                byte[]? audioFromApi = await _tts.Value.TryGetAudioBytesFromTikTokAPI(chat);
                token.ThrowIfCancellationRequested();
                if (audioFromApi == null)
                {
                    _chatStatus.ChatFeedbackTxt = "Error getting TTS from online servers.";
                    return;
                }

                _chatStatus.ChatFeedbackTxt = "TTS is playing...";

                await _tts.Value.PlayTikTokAudioAsSpeechAsync(
                    audioFromApi,
                    _ttsAudio.SelectedPlaybackOutputDevice.ID,
                    token);

                _chatStatus.ChatFeedbackTxt = resent
                    ? "Chat was sent again with TTS."
                    : "Chat was sent with TTS.";
            }
            finally
            {
                lock (_tokensLock)
                    _activeCancellationTokens.Remove(cts2);
                cts2.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _chatStatus.ChatFeedbackTxt = "TTS cancelled";
        }
        catch (Exception ex)
        {
            _chatStatus.ChatFeedbackTxt = "Error sending a chat with TTS";
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
