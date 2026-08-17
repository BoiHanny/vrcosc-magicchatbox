using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum SavedWriteOutcome
{
    Kept,
    Replaced,
    NotSaved,
}

public sealed record SavedWriteRow(string Name, double Sent, double Saved, SavedWriteOutcome Outcome);

public sealed record SavedWriteReport(IReadOnlyList<SavedWriteRow> Rows)
{
    public static readonly SavedWriteReport Empty = new(Array.Empty<SavedWriteRow>());

    public int Kept => Rows.Count(r => r.Outcome == SavedWriteOutcome.Kept);

    public int Replaced => Rows.Count(r => r.Outcome == SavedWriteOutcome.Replaced);

    public int NotSaved => Rows.Count(r => r.Outcome == SavedWriteOutcome.NotSaved);

    public int Compared => Kept + Replaced;

    public string Summary
    {
        get
        {
            if (Rows.Count == 0)
                return "Nothing to compare yet. Change something above, then switch avatar or close VRChat so it writes its file.";

            if (Compared == 0)
                return $"VRChat has not saved any of the {Rows.Count} settings this app changed, so none of them persist.";

            if (Replaced == 0)
                return $"VRChat kept what this app set on all {Kept} of the settings it saves. Values written over OSC do persist.";

            if (Kept == 0)
                return $"VRChat replaced what this app set on all {Replaced} of the settings it saves. Values written over OSC do not persist.";

            return $"VRChat kept {Kept} of {Compared} and replaced {Replaced}. Persistence is not reliable.";
        }
    }
}

public static class SavedWriteAudit
{
    public const double Tolerance = 0.001;

    public static SavedWriteReport Compare(
        IReadOnlyDictionary<string, double> sent,
        LocalAvatarState? saved)
    {
        ArgumentNullException.ThrowIfNull(sent);

        if (sent.Count == 0)
            return SavedWriteReport.Empty;

        var byName = new Dictionary<string, double>(StringComparer.Ordinal);

        if (saved != null)
        {
            foreach (LocalAvatarValue value in saved.Values)
                byName[value.Name] = value.Value;
        }

        var rows = new List<SavedWriteRow>();

        foreach (KeyValuePair<string, double> entry in sent.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!byName.TryGetValue(entry.Key, out double stored))
            {
                rows.Add(new SavedWriteRow(entry.Key, entry.Value, 0, SavedWriteOutcome.NotSaved));
                continue;
            }

            rows.Add(new SavedWriteRow(
                entry.Key,
                entry.Value,
                stored,
                Math.Abs(stored - entry.Value) <= Tolerance
                    ? SavedWriteOutcome.Kept
                    : SavedWriteOutcome.Replaced));
        }

        return new SavedWriteReport(rows);
    }
}
