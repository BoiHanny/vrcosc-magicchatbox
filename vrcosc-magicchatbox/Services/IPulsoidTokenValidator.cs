using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface IPulsoidTokenValidator
{
    Task<bool> ValidateTokenAsync(string accessToken);
}
