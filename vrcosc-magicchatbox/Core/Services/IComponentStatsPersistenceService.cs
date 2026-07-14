namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages component stats persistence.
/// Delegates to ComponentStatsModule which owns its own file (ComponentStatsV1.json).
/// </summary>
public interface IComponentStatsPersistenceService
{
    void LoadComponentStats();
    void SaveComponentStats();
}
