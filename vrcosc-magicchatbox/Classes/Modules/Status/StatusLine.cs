using vrcosc_magicchatbox.Core.Osc.Text;

namespace vrcosc_magicchatbox.Classes.Modules.Status;

/// <summary>
/// Composes the status segment - the user's own words, behind the app's icon when that is switched
/// on - and cuts it to the room the line has left.
/// </summary>
/// <remarks>
/// The editor accepts a full 144 characters and the icon costs three more, so the longest status a
/// user could save was already a line the chatbox would refuse. Nothing here restyles the message:
/// this is the one segment whose text is the user's own, so the icon is what gives way first.
/// </remarks>
public static class StatusLine
{
    public static string Compose(string? message, string? icon, bool prefixIcon, int budget)
    {
        string plain = new SegmentWriter().Field(OscText.Value(message)).Text;
        if (plain.Length == 0)
            return string.Empty;

        if (!prefixIcon || string.IsNullOrWhiteSpace(icon))
            return SegmentWriter.Fit(budget, plain);

        string decorated = new SegmentWriter().Field(OscText.Raw(icon), OscText.Value(message)).Text;

        // Dropping the icon buys back the whole prefix in one go, which is cheaper than eating the
        // last words of a message somebody wrote on purpose.
        return SegmentWriter.Fit(budget, decorated, plain);
    }
}
