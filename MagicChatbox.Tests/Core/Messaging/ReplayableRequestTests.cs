using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Messaging;
using Xunit;

namespace MagicChatbox.Tests.Core.Messaging;

/// <summary>
/// A request raised before anyone is listening yet, and whether it survives to be heard.
/// </summary>
/// <remarks>
/// This is the exact shape of the bug behind Options' scroll-to-section: setting the selected page
/// index realizes the page synchronously, but the page does not subscribe until its own Loaded
/// fires, on a later, lower-priority dispatcher pass. A request raised in that gap used to reach an
/// event with nobody on it and vanish. WPF's own dispatcher priorities are what make the gap real,
/// but nothing here needs a dispatcher to prove the fix - the race is just "raise, then subscribe",
/// and that ordering is exactly what these tests drive.
/// </remarks>
public class ReplayableRequestTests
{
    [Fact]
    public void A_request_raised_before_anyone_subscribes_reaches_the_first_subscriber()
    {
        var request = new ReplayableRequest<string>();
        request.Raise("Settings_TrackerBattery");

        string? received = null;
        request.Requested += value => received = value;

        Assert.Equal("Settings_TrackerBattery", received);
    }

    [Fact]
    public void A_request_raised_after_a_subscriber_is_already_attached_is_not_replayed_again()
    {
        var request = new ReplayableRequest<string>();
        var seen = new List<string>();
        request.Requested += seen.Add;

        request.Raise("Settings_Weather");

        Assert.Equal(new[] { "Settings_Weather" }, seen);
    }

    [Fact]
    public void Only_the_most_recent_unheard_request_is_replayed()
    {
        // Two deep links clicked in quick succession before the page exists at all - only the
        // second one is where the user actually meant to land.
        var request = new ReplayableRequest<string>();
        request.Raise("Settings_Weather");
        request.Raise("Settings_TrackerBattery");

        string? received = null;
        request.Requested += value => received = value;

        Assert.Equal("Settings_TrackerBattery", received);
    }

    [Fact]
    public void A_replayed_request_is_not_delivered_a_second_time_to_a_later_subscriber()
    {
        var request = new ReplayableRequest<string>();
        request.Raise("Settings_Discord");

        var first = new List<string>();
        request.Requested += first.Add;
        Assert.Equal(new[] { "Settings_Discord" }, first);

        var second = new List<string>();
        request.Requested += second.Add;
        Assert.Empty(second);
    }

    [Fact]
    public void Unsubscribing_stops_further_delivery()
    {
        var request = new ReplayableRequest<string>();
        var seen = new List<string>();
        void Handler(string value) => seen.Add(value);

        request.Requested += Handler;
        request.Requested -= Handler;
        request.Raise("Settings_Spotify");

        Assert.Empty(seen);
    }

    [Fact]
    public void With_no_subscriber_ever_a_raise_is_a_silent_no_op()
    {
        var request = new ReplayableRequest<string>();
        var exception = Record.Exception(() => request.Raise("Settings_OpenAI"));

        Assert.Null(exception);
    }
}
