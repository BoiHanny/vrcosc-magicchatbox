using vrcosc_magicchatbox.Services.Scope;

namespace vrcosc_magicchatbox.Core.Integrations;

public interface IIntegrationGate
{
    bool Permits(string integrationKey);

    bool PermitsSending();

    bool TryDescribe(string integrationKey, out ScopeDecision decision);
}

public sealed class AlwaysOpenIntegrationGate : IIntegrationGate
{
    public static readonly AlwaysOpenIntegrationGate Instance = new();

    public bool Permits(string integrationKey) => true;

    public bool PermitsSending() => true;

    public bool TryDescribe(string integrationKey, out ScopeDecision decision)
    {
        decision = null!;
        return false;
    }
}
