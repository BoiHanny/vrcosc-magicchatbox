using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Services;

public interface IVersionService
{
    string GetApplicationVersion();

    Task CheckForUpdateAndWait(bool checkAgain = false);
}
