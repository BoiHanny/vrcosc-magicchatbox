using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Narrow interface for Pulsoid OAuth token validation.
/// Extracted from PulsoidOAuthHandler so that transport-layer code
/// (PulsoidApiClient) doesn't depend on the full browser-based OAuth flow.
/// </summary>
public interface IPulsoidTokenValidator
{
    /// <summary>
    /// Validates the given access token against the Pulsoid API.
    /// Returns true if the token is valid and has the required scopes.
    /// Returns true on transient network errors (optimistic).
    /// </summary>
    Task<bool> ValidateTokenAsync(string accessToken);
}
