using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.State;

public sealed class VoicemodDisplayStateTests
{
    [Fact]
    public void ParameterRevision_ChangesForFullAndIncrementalServerUpdates()
    {
        var display = new VoicemodDisplayState();
        var parameter = new VoicemodVoiceParameter(
            "mix",
            "Mix",
            DefaultValue: 0.5,
            Minimum: 0,
            Maximum: 1,
            Value: 0.5,
            DisplayNormalized: true,
            TypeController: 0);

        display.ReplaceParameters("robot", new[] { parameter });
        int afterFullUpdate = display.ParametersRevision;

        display.UpdateVoiceParameter("mix", 0.8);

        Assert.True(afterFullUpdate > 0);
        Assert.Equal(afterFullUpdate + 1, display.ParametersRevision);
        Assert.Equal(0.8, display.Parameters[0].Value);
    }
}
