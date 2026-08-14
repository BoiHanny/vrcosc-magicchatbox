using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class IntelliChatModelCapabilitiesTests
{
    [Theory]
    [InlineData(IntelliGPTModel.gpt5_2)]
    [InlineData(IntelliGPTModel.gpt5_1)]
    [InlineData(IntelliGPTModel.gpt5)]
    [InlineData(IntelliGPTModel.gpt5_mini)]
    [InlineData(IntelliGPTModel.gpt5_nano)]
    [InlineData(IntelliGPTModel.o1)]
    [InlineData(IntelliGPTModel.o1_mini)]
    [InlineData(IntelliGPTModel.o3)]
    [InlineData(IntelliGPTModel.o3_mini)]
    public void Reasoning_models_reject_sampling_and_need_a_real_output_budget(IntelliGPTModel model)
    {
        // Reasoning models error on temperature/top-p/penalties and spend output tokens on hidden
        // reasoning first, so a chat-sized budget returns an empty reply.
        var capabilities = IntelliChatModule.GetModelCapabilities(model);

        Assert.False(capabilities.SupportsSamplingParams);
        Assert.True(capabilities.MinOutputTokens >= 1000);
    }

    [Theory]
    [InlineData(IntelliGPTModel.gpt4_1)]
    [InlineData(IntelliGPTModel.gpt4_1_mini)]
    [InlineData(IntelliGPTModel.gpt4_1_nano)]
    [InlineData(IntelliGPTModel.gpt4o)]
    [InlineData(IntelliGPTModel.gpt4omini)]
    public void Classic_sampling_models_keep_their_params_and_budget(IntelliGPTModel model)
    {
        var capabilities = IntelliChatModule.GetModelCapabilities(model);

        Assert.True(capabilities.SupportsSamplingParams);
        Assert.Equal(0, capabilities.MinOutputTokens);
    }

    [Fact]
    public void A_number_that_is_not_a_model_at_all_still_resolves()
    {
        // Settings files are edited by hand and survive across versions.
        var capabilities = IntelliChatModule.GetModelCapabilities((IntelliGPTModel)9999);

        Assert.True(capabilities.SupportsSamplingParams);
        Assert.Equal(0, capabilities.MinOutputTokens);
    }

    [Fact]
    public void The_default_model_never_gets_sampling_params_it_would_reject()
    {
        // Every IntelliChat feature defaults to gpt5_nano, so this is the out-of-the-box request.
        var options = IntelliChatModule.BuildChatOptions(
            IntelliGPTModel.gpt5_nano,
            maxOutputTokens: 60,
            temperature: 0.7f,
            topP: 1f,
            frequencyPenalty: 0.3f,
            presencePenalty: 0.2f);

        Assert.Null(options.Temperature);
        Assert.Null(options.TopP);
        Assert.Null(options.FrequencyPenalty);
        Assert.Null(options.PresencePenalty);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(60)]
    [InlineData(120)]
    public void A_starvation_budget_is_raised_for_reasoning_models(int configured)
    {
        // The feature budgets (3-120) are all smaller than what a reasoning model burns on
        // reasoning alone, so they get lifted to the model's minimum.
        var options = IntelliChatModule.BuildChatOptions(IntelliGPTModel.gpt5_nano, maxOutputTokens: configured);

        Assert.Equal(1000, options.MaxOutputTokenCount);
    }

    [Fact]
    public void A_generous_budget_is_left_alone_for_reasoning_models()
    {
        var options = IntelliChatModule.BuildChatOptions(IntelliGPTModel.gpt5, maxOutputTokens: 4000);

        Assert.Equal(4000, options.MaxOutputTokenCount);
    }

    [Fact]
    public void Sampling_models_keep_their_params_and_exact_budget()
    {
        var options = IntelliChatModule.BuildChatOptions(
            IntelliGPTModel.gpt4o,
            maxOutputTokens: 60,
            temperature: 0.7f,
            topP: 1f,
            frequencyPenalty: 0.3f,
            presencePenalty: 0.2f);

        Assert.Equal(60, options.MaxOutputTokenCount);
        Assert.Equal(0.7f, options.Temperature);
        Assert.Equal(1f, options.TopP);
        Assert.Equal(0.3f, options.FrequencyPenalty);
        Assert.Equal(0.2f, options.PresencePenalty);
    }

    [Fact]
    public void Zero_penalties_stay_unset_for_sampling_models()
    {
        var options = IntelliChatModule.BuildChatOptions(IntelliGPTModel.gpt4omini, maxOutputTokens: 60, temperature: 0.3f);

        Assert.Null(options.FrequencyPenalty);
        Assert.Null(options.PresencePenalty);
    }
}
