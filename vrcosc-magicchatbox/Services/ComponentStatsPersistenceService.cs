using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Delegates to ComponentStatsModule which owns its own persistence (ComponentStatsV1.json).
/// DataController.Save/LoadComponentStats (ComponentStats.json) is legacy dead code.
/// </summary>
public sealed class ComponentStatsPersistenceService : IComponentStatsPersistenceService
{
    private readonly ComponentStatsViewModel _statsVm;

    public ComponentStatsPersistenceService(ComponentStatsViewModel statsVm)
    {
        _statsVm = statsVm;
    }

    public void LoadComponentStats()
    {
        _statsVm.Module.LoadComponentStats();
    }

    public void SaveComponentStats()
    {
        _statsVm.Module.SaveComponentStats();
    }
}
