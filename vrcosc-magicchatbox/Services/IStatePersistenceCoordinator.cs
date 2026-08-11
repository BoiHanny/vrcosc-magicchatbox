using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface IStatePersistenceCoordinator
{
    void PersistAllState();

    Task PrepareForShutdownAsync();
}
