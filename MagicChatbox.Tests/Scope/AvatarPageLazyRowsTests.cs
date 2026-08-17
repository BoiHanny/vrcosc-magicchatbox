using MagicChatbox.Vocabulary;
using System;
using System.Collections.Generic;
using System.Linq;
using MagicChatbox.Vrc;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.ViewModels.Avatar;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// Nothing on this page virtualises and it cannot: a ScrollViewer over a StackPanel measures with
// infinite height, so a virtualising panel would realise every row anyway. Collapsed groups are
// therefore the whole of the fix -- and a collapsed group that has already built its rows saves nothing.
public class AvatarPageLazyRowsTests
{
    private sealed class NullSink : IAvatarParameterSink
    {
        public void Set(string name, bool value) { }

        public void Set(string name, int value) { }

        public void Set(string name, float value) { }

        public void Pulse(string name, int milliseconds = 150) { }
    }

    private static AvatarControlRow Row(string name) =>
        new(name, name, SignalKind.Bool, true, AvatarWidget.Toggle, 0, false, false);

    private static (AvatarControlGroupViewModel Group, List<string> Built) Group(bool expanded, params string[] names)
    {
        var built = new List<string>();
        var source = names.Select(Row).ToList();

        var group = new AvatarControlGroupViewModel(
            "Toggles",
            "Toggles",
            source,
            row =>
            {
                built.Add(row.Name);
                return new AvatarControlRowViewModel(row, new NullSink());
            },
            expanded);

        return (group, built);
    }

    [Fact]
    public void A_collapsed_group_builds_nothing_at_all()
    {
        var (group, built) = Group(expanded: false, "A", "B", "C");

        Assert.Empty(built);
        Assert.Empty(group.Rows);
    }

    [Fact]
    public void A_collapsed_group_still_knows_how_many_it_holds()
    {
        // The header has to say the count without building the rows, or collapsing saves nothing.
        var (group, built) = Group(expanded: false, "A", "B", "C");

        Assert.Equal(3, group.RowCount);
        Assert.Equal("Toggles  (3)", group.Header);
        Assert.Empty(built);
    }

    [Fact]
    public void Opening_a_group_builds_its_rows_once()
    {
        var (group, built) = Group(expanded: false, "A", "B");

        group.IsExpanded = true;

        Assert.Equal(new[] { "A", "B" }, built);
        Assert.Equal(2, group.Rows.Count);
    }

    [Fact]
    public void Opening_and_closing_does_not_build_them_again()
    {
        var (group, built) = Group(expanded: false, "A", "B");

        group.IsExpanded = true;
        group.IsExpanded = false;
        group.IsExpanded = true;

        Assert.Equal(2, built.Count);
    }

    [Fact]
    public void A_group_that_starts_open_builds_immediately()
    {
        var (group, built) = Group(expanded: true, "A");

        Assert.Single(built);
        Assert.True(group.IsExpanded);
    }
}

public class SensitiveParameterTests
{
    [Theory]
    [InlineData("Config/apiKey")]
    [InlineData("MySecretThing")]
    [InlineData("auth/BEARER")]
    [InlineData("private_key")]
    [InlineData("user/password")]
    public void A_parameter_whose_name_looks_like_a_credential_is_never_rendered(string name)
    {
        Assert.True(AvatarControlCatalog.IsSensitiveName(name));
    }

    [Theory]
    [InlineData("Toggles/Hat")]
    [InlineData("VRCEmote")]
    [InlineData("MCB/Cfg/HeartRate")]
    [InlineData("")]
    [InlineData(null)]
    public void An_ordinary_parameter_is_left_alone(string name)
    {
        Assert.False(AvatarControlCatalog.IsSensitiveName(name));
    }

    [Fact]
    public void A_credential_shaped_name_is_dropped_from_the_built_view()
    {
        var schema = new AvatarSchemaSnapshot("avtr_one", 1, DateTime.UtcNow,
        [
            new VrcParameterDeclaration("Toggles/Hat", SignalKind.Bool, SignalValue.Bool(false), true),
            new VrcParameterDeclaration("Twitch/oauth_token", SignalKind.Bool, SignalValue.Bool(false), true),
        ]);

        AvatarControlView view = AvatarControlCatalog.Build(schema);

        Assert.DoesNotContain(
            view.Groups.SelectMany(g => g.Rows),
            r => r.Name.Contains("token", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(view.Groups.SelectMany(g => g.Rows), r => r.Name == "Toggles/Hat");
    }
}
