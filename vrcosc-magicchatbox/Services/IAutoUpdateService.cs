using System.Collections.Generic;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Updates;

namespace vrcosc_magicchatbox.Services;

public enum StartupUpdateOutcome
{
    Continue,
    HandingOff,
}

public interface IAutoUpdateService
{
    IReadOnlyList<string> BlockedVersions { get; }

    StartupUpdateOutcome PrepareForStartup(bool launchedBySteamVr);

    void ReportStartupHealthy();

    Task ConsiderAsync(UpdateVerdict verdict);
}
