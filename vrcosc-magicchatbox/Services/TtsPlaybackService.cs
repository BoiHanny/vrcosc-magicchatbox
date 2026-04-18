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
        foreach (var cts in _activeCancellationTokens)
            cts.Cancel();
        _activeCancellationTokens.Clear();
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
            {
                foreach (var cts in _activeCancellationTokens)
                    cts.Cancel();
                _activeCancellationTokens.Clear();
            }

            byte[]? audioFromApi = await _tts.Value.GetAudioBytesFromTikTokAPI(chat);
            if (audioFromApi == null)
            {
                _chatStatus.ChatFeedbackTxt = "Error getting TTS from online servers.";
                return;
            }

            var cts2 = new CancellationTokenSource();
            _activeCancellationTokens.Add(cts2);
            _chatStatus.ChatFeedbackTxt = "TTS is playing...";

            await _tts.Value.PlayTikTokAudioAsSpeechAsync(
                audioFromApi,
                _ttsAudio.SelectedPlaybackOutputDevice.ID,
                cts2.Token);

            _chatStatus.ChatFeedbackTxt = resent
                ? "Chat was sent again with TTS."
                : "Chat was sent with TTS.";

            _activeCancellationTokens.Remove(cts2);
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
