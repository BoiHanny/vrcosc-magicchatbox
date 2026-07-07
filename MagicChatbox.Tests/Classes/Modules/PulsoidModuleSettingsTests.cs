using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class PulsoidModuleSettingsTests
{
    [Fact]
    public void AccessTokenOAuth_RoundTrips_ThroughEncryptedJsonProperty()
    {
        var settings = new PulsoidModuleSettings { AccessTokenOAuth = "secret-token" };

        string json = JsonConvert.SerializeObject(settings);
        var loaded = JsonConvert.DeserializeObject<PulsoidModuleSettings>(json);

        Assert.DoesNotContain("secret-token", json);
        Assert.NotNull(loaded);
        Assert.Equal("secret-token", loaded.AccessTokenOAuth);
    }
}
