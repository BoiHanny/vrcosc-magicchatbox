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

            var cts2 = new CancellationTokenSource();
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
                    // Both call sites gate TTS on a prior successful PopulateOutputDevices(), which
                    // assigns this before the API round-trip above has a chance to complete.
                    _ttsAudio.SelectedPlaybackOutputDevice!.ID,
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
