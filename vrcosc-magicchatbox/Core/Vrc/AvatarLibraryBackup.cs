using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed class AvatarLibraryBackupEntry
{
    public string AvatarId { get; set; } = string.Empty;

    public string AvatarName { get; set; } = string.Empty;

    public double EyeHeight { get; set; }

    public Dictionary<string, double> Values { get; set; } = new();
}

public sealed class AvatarLibraryBackupFile
{
    public const string ExpectedKind = "mcb.avatarlibrary";
    public const int CurrentSchema = 1;

    public string Kind { get; set; } = ExpectedKind;

    public int Schema { get; set; } = CurrentSchema;

    public DateTime TakenUtc { get; set; }

    public List<AvatarLibraryBackupEntry> Avatars { get; set; } = new();
}

public static class AvatarLibraryBackup
{
    public const int MaxAvatars = 4096;

    public static AvatarLibraryBackupFile Build(
        IEnumerable<LocalAvatarState> states,
        DateTime takenUtc,
        Func<string, string>? nameFor = null)
    {
        ArgumentNullException.ThrowIfNull(states);

        var file = new AvatarLibraryBackupFile { TakenUtc = takenUtc };

        foreach (LocalAvatarState state in states.Take(MaxAvatars))
        {
            if (state.Values.Count == 0)
                continue;

            var entry = new AvatarLibraryBackupEntry
            {
                AvatarId = state.AvatarId,
                AvatarName = nameFor?.Invoke(state.AvatarId) ?? string.Empty,
                EyeHeight = state.EyeHeight,
            };

            foreach (LocalAvatarValue value in state.Values)
            {
                if (AvatarControlCatalog.IsVrchatOwned(value.Name))
                    continue;

                entry.Values[value.Name] = value.Value;
            }

            if (entry.Values.Count > 0)
                file.Avatars.Add(entry);
        }

        return file;
    }

    public static bool IsUsable(AvatarLibraryBackupFile? file, out string detail)
    {
        if (file == null)
        {
            detail = "That file could not be read.";
            return false;
        }

        if (!string.Equals(file.Kind, AvatarLibraryBackupFile.ExpectedKind, StringComparison.Ordinal))
        {
            detail = "That is not an avatar backup from this app.";
            return false;
        }

        if (file.Schema > AvatarLibraryBackupFile.CurrentSchema)
        {
            detail = "That backup was written by a newer version of MagicChatbox.";
            return false;
        }

        if (file.Avatars.Count == 0)
        {
            detail = "That backup has no avatars in it.";
            return false;
        }

        detail = string.Empty;
        return true;
    }

    public static LocalAvatarState? StateFor(AvatarLibraryBackupFile file, string avatarId)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(avatarId))
            return null;

        foreach (AvatarLibraryBackupEntry entry in file.Avatars)
        {
            if (!string.Equals(entry.AvatarId, avatarId, StringComparison.Ordinal))
                continue;

            return new LocalAvatarState(
                entry.AvatarId,
                entry.EyeHeight,
                false,
                entry.Values.Select(v => new LocalAvatarValue(v.Key, v.Value)).ToList(),
                file.TakenUtc);
        }

        return null;
    }
}
