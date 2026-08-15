using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public enum PulsoidTokenValidation
{
    Valid,

    Invalid,

    Unknown
}

public interface IPulsoidTokenValidator
{
    Task<PulsoidTokenValidation> ValidateTokenAsync(string accessToken);
}
