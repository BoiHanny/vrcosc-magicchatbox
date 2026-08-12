using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.SpeechToText;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.SpeechToText;

public class SpeechToTextModelsTests
{
    private static string ApiId(IntelliGPTModel model)
    {
        var field = typeof(IntelliGPTModel).GetField(model.ToString())!;
        var attribute = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attribute[0].Description;
    }

    [Fact]
    public void Saved_selections_keep_their_numbers_forever()
    {
        // The selected model is persisted as its number. Renumbering any of these moves existing
        // users onto a different model without telling them - a file saying whisper-1 could ask for
        // a chat model, and every transcription would fail. This test is the contract.
        Assert.Equal(14, (int)IntelliGPTModel.whisper1);
        Assert.Equal(15, (int)IntelliGPTModel.gpt_4o_mini_transcribe);
        Assert.Equal(16, (int)IntelliGPTModel.gpt_4o_transcribe);
        Assert.Equal(17, (int)IntelliGPTModel.gpt_4o_transcribe_diarize);
        Assert.Equal(18, (int)IntelliGPTModel.Moderation_Latest);
        Assert.Equal(19, (int)IntelliGPTModel.gpt_transcribe);
    }

    [Fact]
    public void Every_model_has_its_own_number()
    {
        var values = Enum.GetValues<IntelliGPTModel>().Cast<int>().ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void The_recommended_model_is_the_one_OpenAI_recommends()
    {
        Assert.Equal("gpt-transcribe", ApiId(SpeechToTextModels.Recommended));
    }

    [Fact]
    public void The_recommended_model_is_offered_first()
    {
        Assert.Equal(SpeechToTextModels.Recommended, SpeechToTextModels.Ordered[0]);
    }

    [Fact]
    public void Only_models_the_transcription_endpoint_accepts_are_offered()
    {
        // The realtime models are for streaming sessions. This module records a clip and uploads it,
        // so offering them would only produce failures.
        var ids = SpeechToTextModels.Ordered.Select(ApiId).ToList();

        Assert.DoesNotContain("gpt-live-transcribe", ids);
        Assert.DoesNotContain("gpt-realtime-whisper", ids);
    }

    [Fact]
    public void The_offered_list_is_exactly_what_the_api_takes_today()
    {
        var expected = new[]
        {
            "gpt-transcribe",
            "gpt-4o-transcribe",
            "gpt-4o-mini-transcribe",
            "gpt-4o-transcribe-diarize",
            "whisper-1",
        };

        Assert.Equal(expected, SpeechToTextModels.Ordered.Select(ApiId).ToArray());
    }

    private static string ModelType(IntelliGPTModel model)
    {
        var field = typeof(IntelliGPTModel).GetField(model.ToString())!;
        var attribute = (ModelTypeInfoAttribute[])field.GetCustomAttributes(typeof(ModelTypeInfoAttribute), false);
        return attribute.Length > 0 ? attribute[0].ModelType : string.Empty;
    }

    [Fact]
    public void Everything_offered_is_tagged_as_a_transcription_model()
    {
        // The tag drives filtering elsewhere in the app, so a chat model slipping into this list
        // would go unnoticed until somebody tried to talk to it.
        foreach (var model in SpeechToTextModels.Ordered)
            Assert.Equal("STT", ModelType(model));
    }

    [Fact]
    public void Every_transcription_model_in_the_enum_is_offered()
    {
        // Otherwise a model can be added to the enum, tagged STT, and never appear in the picker.
        var tagged = Enum.GetValues<IntelliGPTModel>().Where(m => ModelType(m) == "STT");

        Assert.Equal(
            tagged.OrderBy(m => (int)m),
            SpeechToTextModels.Ordered.OrderBy(m => (int)m));
    }

    [Fact]
    public void A_still_offered_choice_is_left_alone_including_the_older_ones()
    {
        // whisper-1 is old but still the only one that can translate to English or produce subtitles,
        // so somebody who picked it deliberately keeps it.
        foreach (var model in SpeechToTextModels.Ordered)
            Assert.Equal(model, SpeechToTextModels.Resolve(model));
    }

    [Fact]
    public void A_chat_model_left_in_the_settings_falls_back_instead_of_failing_every_transcription()
    {
        Assert.Equal(SpeechToTextModels.Recommended, SpeechToTextModels.Resolve(IntelliGPTModel.gpt5_2));
        Assert.Equal(SpeechToTextModels.Recommended, SpeechToTextModels.Resolve(IntelliGPTModel.Moderation_Latest));
    }

    [Fact]
    public void A_number_that_is_not_a_model_at_all_still_resolves()
    {
        // Settings files are edited by hand and survive across versions.
        Assert.Equal(SpeechToTextModels.Recommended, SpeechToTextModels.Resolve((IntelliGPTModel)9999));
    }

    [Fact]
    public void Nothing_deprecated_is_offered()
    {
        // gpt-4o-mini-transcribe-2025-03-20 is retired on 20 Jan 2027. The app uses undated aliases,
        // and this makes sure a dated snapshot never creeps into the list.
        Assert.All(SpeechToTextModels.Ordered, m => Assert.DoesNotContain("2025-", ApiId(m)));
    }
}
