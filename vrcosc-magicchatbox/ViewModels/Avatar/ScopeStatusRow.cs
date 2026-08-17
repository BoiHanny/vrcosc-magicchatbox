using MagicChatbox.Scope;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services.Scope;

namespace vrcosc_magicchatbox.ViewModels;

public sealed record ScopeStatusRow(string Parameter, string Detail, string Mark)
{
    public static ScopeStatusRow From(ScopeDecision decision)
    {
        string what = decision.Target.Kind == ScopeTargetKind.Integration
            ? IntegrationTileCatalog.DisplayNameFor(decision.Target.Key)
            : "Sending";

        string sentence = decision.Sentence;

        string detail = decision.Verdict switch
        {
            ScopeVerdict.Blocked => $"{what} is held off — {Reason(decision)}",
            ScopeVerdict.Settling => $"{what} is waiting for {sentence} to settle",
            _ => $"{what} is allowed — {sentence}",
        };

        string mark = decision.Verdict switch
        {
            ScopeVerdict.Blocked => "✕",
            ScopeVerdict.Settling => "…",
            _ => "✓",
        };

        return new ScopeStatusRow(decision.RuleName, detail, mark);
    }

    private static string Reason(ScopeDecision decision)
    {
        string because = ScopeMirror.Because(decision.Outcome, decision.Block, names: null);
        return because.Length > 0 ? because : decision.Sentence;
    }
}
