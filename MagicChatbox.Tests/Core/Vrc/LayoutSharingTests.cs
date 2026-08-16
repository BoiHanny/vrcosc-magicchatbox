using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Core.Vrc.Sharing;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Importing a layout is importing a stranger's data. The format is defined by what it cannot say:
// no OSC addresses, no settings, no free text that reaches VRChat under the importer's name, no
// secrets. Those are absent from the type rather than filtered at runtime, because an absent field
// cannot be argued with and a dialog can be clicked through.
public class LayoutSharingTests
{
    private static readonly string[] AllowedFields =
    [
        "Kind", "Schema", "Title", "Description", "Author", "License", "Tags", "Requires",
    ];

    private static readonly string[] AllowedRequirementFields =
    [
        "Name", "Type", "Optional", "Purpose",
    ];

    private static AvatarSchemaSnapshot Schema(params (string Name, SignalKind Kind, bool Writable)[] parameters)
        => new(
            "avtr_test", 1, DateTime.UtcNow,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, p.Kind, SignalValue.Bool(false), p.Writable))
                .ToList());

    private static LayoutDocument Sample() => new()
    {
        Title = "Heart rate",
        Author = "Someone",
        Requires =
        {
            new LayoutRequirement { Name = "HR", Type = "Int", Purpose = "Beats per minute" },
            new LayoutRequirement { Name = "isHRBeat", Type = "Bool", Optional = true, Purpose = "Pulse" },
        },
    };

    [Fact]
    public void The_document_carries_only_the_fields_that_were_reviewed()
    {
        // This test IS the security boundary. Adding a field to the type without editing this list
        // fails the build, which makes the review the gate rather than somebody's memory.
        var actual = typeof(LayoutDocument)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedFields.OrderBy(n => n, StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void A_requirement_carries_only_reviewed_fields_too()
    {
        var actual = typeof(LayoutRequirement)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedRequirementFields.OrderBy(n => n, StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void An_imported_name_can_never_become_an_arbitrary_OSC_address()
    {
        // The app sends /input/Voice, so one imported string beginning with a slash would unmute a
        // stranger's microphone. Names only, and the prefix is added by us.
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("/input/Voice", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("/chatbox/input", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("x/avatar/parameters/y", out _));
    }

    [Fact]
    public void An_ordinary_parameter_name_still_resolves()
    {
        Assert.True(AvatarParameterAddress.TryResolveUntrusted("Toggles/Hat", out string address));
        Assert.Equal("/avatar/parameters/Toggles/Hat", address);
    }

    [Fact]
    public void Names_VRChat_cannot_address_are_refused()
    {
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("Has Space", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("Star*", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("Brack[et]", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted("null\0byte", out _));
        Assert.False(AvatarParameterAddress.TryResolveUntrusted(new string('x', 400), out _));
    }

    [Fact]
    public void A_trusted_caller_keeps_the_behaviour_it_always_had()
    {
        // The camera flash setting has always stored a whole address and is user editable.
        Assert.Equal(
            "/avatar/parameters/CameraFlash",
            AvatarParameterAddress.ResolveTrusted("/avatar/parameters/CameraFlash"));
    }

    [Fact]
    public void A_layout_round_trips_through_json()
    {
        LayoutParseResult result = LayoutCodec.Read(LayoutCodec.Write(Sample()));

        Assert.True(result.Ok);
        Assert.Equal("Heart rate", result.Document!.Title);
        Assert.Equal(2, result.Document.Requires.Count);
    }

    [Fact]
    public void A_layout_round_trips_through_a_share_code()
    {
        // A text code posts inline in Discord where a file attachment does not.
        string code = LayoutCodec.ToCode(Sample());

        Assert.StartsWith(LayoutCodec.CodePrefix, code, StringComparison.Ordinal);

        LayoutParseResult result = LayoutCodec.FromCode(code);

        Assert.True(result.Ok);
        Assert.Equal(2, result.Document!.Requires.Count);
    }

    [Fact]
    public void A_damaged_code_is_refused_rather_than_throwing()
    {
        Assert.False(LayoutCodec.FromCode("MCBL1-not-base64!!").Ok);
        Assert.False(LayoutCodec.FromCode("hello").Ok);
        Assert.False(LayoutCodec.FromCode(null).Ok);
    }

    [Fact]
    public void Something_that_is_not_a_layout_is_refused_by_kind()
    {
        LayoutParseResult result = LayoutCodec.Read("{\"kind\":\"something.else\",\"schema\":1}");

        Assert.False(result.Ok);
        Assert.Equal(LayoutRejection.WrongKind, result.Rejection);
    }

    [Fact]
    public void A_layout_from_a_newer_version_is_refused_with_an_explanation()
    {
        LayoutParseResult result = LayoutCodec.Read("{\"kind\":\"mcb.layout\",\"schema\":99}");

        Assert.Equal(LayoutRejection.UnsupportedSchema, result.Rejection);
        Assert.Contains("newer version", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_layout_carrying_an_address_is_refused_at_parse()
    {
        string json = "{\"kind\":\"mcb.layout\",\"schema\":1,\"requires\":[{\"name\":\"/input/Voice\",\"type\":\"Bool\"}]}";

        LayoutParseResult result = LayoutCodec.Read(json);

        Assert.False(result.Ok);
        Assert.Equal(LayoutRejection.IllegalName, result.Rejection);
    }

    [Fact]
    public void Unknown_fields_are_ignored_rather_than_binding_to_anything()
    {
        // Forward compatibility, and the reason deserialisation targets a dedicated type rather than
        // a live settings object.
        string json =
            "{\"kind\":\"mcb.layout\",\"schema\":1,\"secOSCIP\":\"10.0.0.5\",\"acceptedTosVersion\":\"9\"," +
            "\"requires\":[{\"name\":\"HR\",\"type\":\"Int\"}]}";

        LayoutParseResult result = LayoutCodec.Read(json);

        Assert.True(result.Ok);
        Assert.Single(result.Document!.Requires);
    }

    [Fact]
    public void An_oversized_document_is_refused_before_it_is_parsed()
    {
        string json = "{\"kind\":\"mcb.layout\",\"schema\":1,\"description\":\""
                      + new string('x', LayoutCodec.MaxBytes + 10) + "\"}";

        Assert.Equal(LayoutRejection.TooLarge, LayoutCodec.Read(json).Rejection);
    }

    [Fact]
    public void A_document_asking_for_absurdly_many_parameters_is_refused()
    {
        // The worst real avatar has 656 drivable parameters against a 160 a second pump, so an
        // uncapped requirement list is a trivial way to make the app unusable.
        var doc = new LayoutDocument();
        for (int i = 0; i < LayoutCodec.MaxRequirements + 5; i++)
            doc.Requires.Add(new LayoutRequirement { Name = "P" + i, Type = "Bool" });

        Assert.Equal(LayoutRejection.TooManyRequirements, LayoutCodec.Read(LayoutCodec.Write(doc)).Rejection);
    }

    [Fact]
    public void Free_text_is_clipped_and_stripped_of_control_characters()
    {
        var doc = new LayoutDocument { Title = "AB" + new string('x', 900) };

        LayoutParseResult result = LayoutCodec.Read(LayoutCodec.Write(doc));

        Assert.True(result.Ok);
        Assert.DoesNotContain('', result.Document!.Title);
        Assert.True(result.Document.Title.Length <= LayoutCodec.MaxTextLength);
    }

    [Fact]
    public void Matching_a_layout_tells_the_user_exactly_what_is_missing()
    {
        LayoutMatchReport report = LayoutCodec.Match(
            Sample(),
            Schema(("HR", SignalKind.Int, true)));

        Assert.Equal(1, report.Present);
        Assert.True(report.Satisfied);
        Assert.Contains(report.Rows, r => r.Name == "isHRBeat" && r.Match == LayoutMatch.Missing);
    }

    [Fact]
    public void A_missing_required_parameter_makes_the_layout_unsatisfied()
    {
        LayoutMatchReport report = LayoutCodec.Match(Sample(), Schema(("Toggles/Hat", SignalKind.Bool, true)));

        Assert.False(report.Satisfied);
        Assert.Equal(1, report.MissingRequired);
    }

    [Fact]
    public void A_parameter_of_the_wrong_type_is_reported_as_such_rather_than_missing()
    {
        LayoutMatchReport report = LayoutCodec.Match(Sample(), Schema(("HR", SignalKind.Bool, true)));

        Assert.Contains(report.Rows, r => r.Name == "HR" && r.Match == LayoutMatch.WrongType);
    }

    [Fact]
    public void A_read_only_parameter_does_not_satisfy_a_requirement()
    {
        LayoutMatchReport report = LayoutCodec.Match(Sample(), Schema(("HR", SignalKind.Int, false)));

        Assert.Contains(report.Rows, r => r.Name == "HR" && r.Match == LayoutMatch.NotWritable);
    }
}
