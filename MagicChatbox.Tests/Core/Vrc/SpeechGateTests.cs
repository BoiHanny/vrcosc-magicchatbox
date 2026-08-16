using System;
using System.Threading;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// VRChat publishes a Voice level on 196 of 197 avatars, read-only and free. It answers "is the user
// talking right now", which is the missing input behind three separate annoyances: speech-to-text
// transcribing the app's own text-to-speech, lyrics overwriting the chatbox mid-sentence, and the
// hot mic nobody notices.
public class SpeechGateTests
{
    [Fact]
    public void Silence_is_not_speech()
    {
        var gate = new SpeechGate();

        gate.ObserveVoice(0);

        Assert.False(gate.IsSpeaking);
    }

    [Fact]
    public void Voice_above_the_floor_is_speech()
    {
        var gate = new SpeechGate();

        gate.ObserveVoice(0.4);

        Assert.True(gate.IsSpeaking);
    }

    [Fact]
    public void A_pause_between_words_does_not_end_the_sentence()
    {
        // Voice drops to zero between syllables. Without the hangover this flaps several times a
        // second and anything gated on it flickers.
        var gate = new SpeechGate(hangover: TimeSpan.FromMilliseconds(400));

        gate.ObserveVoice(0.5);
        gate.ObserveVoice(0);

        Assert.True(gate.IsSpeaking);
    }

    [Fact]
    public void Speech_ends_once_the_quiet_outlasts_the_hangover()
    {
        var gate = new SpeechGate(hangover: TimeSpan.FromMilliseconds(80));

        gate.ObserveVoice(0.5);
        Thread.Sleep(200);
        gate.ObserveVoice(0);

        Assert.False(gate.IsSpeaking);
    }

    [Fact]
    public void A_muted_microphone_is_never_a_hot_mic()
    {
        var gate = new SpeechGate();

        gate.ObserveMute(true);
        gate.ObserveVoice(0.9);

        Assert.True(gate.IsSpeaking);
        Assert.True(gate.IsMuted);
        Assert.False(gate.IsHotMic);
    }

    [Fact]
    public void Talking_while_unmuted_is_a_hot_mic()
    {
        var gate = new SpeechGate();

        gate.ObserveMute(false);
        gate.ObserveVoice(0.9);

        Assert.True(gate.IsHotMic);
    }

    [Fact]
    public void Room_noise_below_the_floor_does_not_register()
    {
        var gate = new SpeechGate(threshold: 0.1);

        gate.ObserveVoice(0.05);

        Assert.False(gate.IsSpeaking);
    }

    [Fact]
    public void An_avatar_change_clears_the_state_rather_than_carrying_it_over()
    {
        var gate = new SpeechGate();

        gate.ObserveVoice(0.8);
        gate.Reset();

        Assert.False(gate.IsSpeaking);
    }
}
