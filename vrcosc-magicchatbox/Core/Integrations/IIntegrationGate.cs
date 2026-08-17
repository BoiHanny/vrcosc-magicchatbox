using vrcosc_magicchatbox.Services.Scope;

namespace vrcosc_magicchatbox.Core.Integrations;

public interface IIntegrationGate
{
    bool Permits(string integrationKey);

    bool PermitsSending();

}

public sealed class AlwaysOpenIntegrationGate : IIntegrationGate
{
    public static readonly AlwaysOpenIntegrationGate Instance = new();

    public bool Permits(string integrationKey) => true;

    public bool PermitsSending() => true;
}
