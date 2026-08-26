using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Updates;

public enum UpdateAction
{
    None,
    Notify,
    AutoInstall,
}

public enum UpdateStanding
{
    UpToDate,
    UpdateAvailable,
    AheadOfReleases,
}

public readonly record struct UpdateOffer(UpdateChannel Channel, string Version, string Url, string Digest)
{
    public static UpdateOffer Absent(UpdateChannel channel)
        => new(channel, string.Empty, string.Empty, string.Empty);

    public bool IsPresent => !string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(Url);
}

public sealed record UpdateVerdict(
    UpdateStanding Standing,
    UpdateAction Action,
    UpdateChannel? Channel,
    string Version,
    string Url,
    string Digest);

public static class UpdateDecision
{
    public static UpdateVerdict Decide(
        string? currentVersion,
        UpdateOffer stable,
        UpdateOffer preRelease,
        UpdateChannelMode stableMode,
        UpdateChannelMode preReleaseMode,
        IReadOnlyCollection<string>? blockedVersions = null)
    {
        List<(UpdateOffer Offer, UpdateChannelMode Mode)> candidates = [];

        Consider(stable, stableMode);
        Consider(preRelease, preReleaseMode);

        // An armed channel must never be starved by a merely-watched one: a newer pre-release
        // sitting on Notify cannot swallow the stable build the user asked to have installed.
        var armed = candidates.Where(candidate => candidate.Mode == UpdateChannelMode.Auto).ToList();
        if (armed.Count > 0)
            return Offer(Best(armed), UpdateAction.AutoInstall);

        if (candidates.Count > 0)
            return Offer(Best(candidates), UpdateAction.Notify);

        return Settled();

        void Consider(UpdateOffer offer, UpdateChannelMode mode)
        {
            if (mode == UpdateChannelMode.Off || !offer.IsPresent)
                return;

            if (ReleaseVersion.Compare(currentVersion, offer.Version) >= 0)
                return;

            if (IsBlocked(offer.Version))
                return;

            candidates.Add((offer, mode));
        }

        bool IsBlocked(string version)
            => blockedVersions is not null
               && blockedVersions.Any(blocked => ReleaseVersion.Compare(blocked, version) == 0);

        UpdateVerdict Offer(UpdateOffer offer, UpdateAction action)
            => new(UpdateStanding.UpdateAvailable, action, offer.Channel, offer.Version, offer.Url, offer.Digest);

        UpdateVerdict Settled()
        {
            bool levelWithPreRelease =
                preReleaseMode != UpdateChannelMode.Off
                && preRelease.IsPresent
                && ReleaseVersion.Compare(currentVersion, preRelease.Version) == 0;

            if (levelWithPreRelease)
                return Idle(UpdateStanding.UpToDate, UpdateChannel.PreRelease);

            bool aheadOfStable = stable.IsPresent && ReleaseVersion.Compare(currentVersion, stable.Version) > 0;
            bool aheadOfPreRelease = !preRelease.IsPresent
                                     || ReleaseVersion.Compare(currentVersion, preRelease.Version) > 0;

            if (aheadOfStable && aheadOfPreRelease)
                return Idle(UpdateStanding.AheadOfReleases, null);

            bool levelWithStable = stable.IsPresent
                                   && ReleaseVersion.Compare(currentVersion, stable.Version) == 0;

            return Idle(UpdateStanding.UpToDate, levelWithStable ? UpdateChannel.Stable : null);
        }

        UpdateVerdict Idle(UpdateStanding standing, UpdateChannel? channel)
            => new(standing, UpdateAction.None, channel, currentVersion ?? string.Empty, string.Empty, string.Empty);
    }

    private static UpdateOffer Best(List<(UpdateOffer Offer, UpdateChannelMode Mode)> candidates)
        => candidates
            .OrderByDescending(candidate => candidate.Offer.Version, VersionOrder.Instance)
            .ThenBy(candidate => candidate.Offer.Channel)
            .First()
            .Offer;

    private sealed class VersionOrder : IComparer<string>
    {
        public static readonly VersionOrder Instance = new();

        public int Compare(string? x, string? y) => ReleaseVersion.Compare(x, y);
    }
}
