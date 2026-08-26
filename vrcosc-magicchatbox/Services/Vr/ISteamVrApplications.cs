namespace vrcosc_magicchatbox.Services.Vr;

public enum SteamVrOutcome
{
    Done,
    SteamVrUnavailable,
    Failed,
}

public readonly record struct SteamVrResult(SteamVrOutcome Outcome, string Detail)
{
    public static SteamVrResult Done() => new(SteamVrOutcome.Done, string.Empty);

    public static SteamVrResult Unavailable(string detail) => new(SteamVrOutcome.SteamVrUnavailable, detail);

    public static SteamVrResult Failed(string detail) => new(SteamVrOutcome.Failed, detail);

    public bool Succeeded => Outcome == SteamVrOutcome.Done;
}

public interface ISteamVrApplications
{
    SteamVrResult Register(string manifestPath, string appKey);

    SteamVrResult Unregister(string manifestPath, string appKey);

    bool IsAutoLaunchEnabled(string appKey);
}
