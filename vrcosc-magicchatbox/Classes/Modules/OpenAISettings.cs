using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted OpenAI credentials and model preferences, stored with encrypted access tokens.
/// </summary>
public partial class OpenAISettings : VersionedSettings
{
    public static string DefaultApiStream { get; } = "b2t8DhYcLcu7Nu0suPcvc8MkHBjZNbEinG/3ybInlUK/5UkyNRVhK145nO7C4Mwhe1Zer1hBcG/F1b5f/BMcNFLXk4K6ozRcK7gHcebJZWnpxEDxjW6DyrZ/si913BPp";

    private string _accessTokenEncrypted = string.Empty;
    private string _accessToken = string.Empty;

    [JsonIgnore]
    public string AccessToken
    {
        get => _accessToken;
        set
        {
            if (SetProperty(ref _accessToken, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _accessToken, ref _accessTokenEncrypted, true);
                OnPropertyChanged(nameof(AccessTokenEncrypted));
            }
        }
    }

    public string AccessTokenEncrypted
    {
        get => _accessTokenEncrypted;
        set
        {
            if (SetProperty(ref _accessTokenEncrypted, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _accessTokenEncrypted, ref _accessToken, false);
                OnPropertyChanged(nameof(AccessToken));
            }
        }
    }

    private string _organizationIDEncrypted = string.Empty;
    private string _organizationID = string.Empty;

    [JsonIgnore]
    public string OrganizationID
    {
        get => _organizationID;
        set
        {
            if (SetProperty(ref _organizationID, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _organizationID, ref _organizationIDEncrypted, true);
                OnPropertyChanged(nameof(OrganizationIDEncrypted));
            }
        }
    }

    public string OrganizationIDEncrypted
    {
        get => _organizationIDEncrypted;
        set
        {
            if (SetProperty(ref _organizationIDEncrypted, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _organizationIDEncrypted, ref _organizationID, false);
                OnPropertyChanged(nameof(OrganizationID));
            }
        }
    }
}
