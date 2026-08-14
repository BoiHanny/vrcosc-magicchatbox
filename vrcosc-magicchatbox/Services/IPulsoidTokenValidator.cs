using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Outcome of asking Pulsoid whether a stored access token is still good.
/// The third state matters: "we could not ask" is not the same as "Pulsoid said no", and
/// collapsing the two is what used to sign users out after a network hiccup at launch.
/// </summary>
public enum PulsoidTokenValidation
{
    /// <summary>Pulsoid answered and accepted the token, and the heart-rate scope is granted.</summary>
    Valid,

    /// <summary>
    /// Pulsoid answered and refused the token (HTTP 401), or the heart-rate scope is missing.
    /// This is the only outcome that may mark a stored credential as dead.
    /// </summary>
    Invalid,

    /// <summary>
    /// The question could not be answered: offline, DNS failure, timeout, 429, 5xx, or any
    /// status Pulsoid does not document as a token problem. The stored token is untouched
    /// and the caller must keep treating the user as signed in.
    /// </summary>
    Unknown
}

public interface IPulsoidTokenValidator
{
    Task<PulsoidTokenValidation> ValidateTokenAsync(string accessToken);
}
