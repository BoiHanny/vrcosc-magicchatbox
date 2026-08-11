namespace vrcosc_magicchatbox.Core.Osc;

public interface IOscProvider
{
    string SortKey { get; }

    string UiKey { get; }

    int Priority { get; }

    bool IsEnabledForCurrentMode(bool isVRRunning);

    OscSegment? TryBuild(OscBuildContext context);
}
