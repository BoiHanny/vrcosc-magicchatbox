using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Privacy;

namespace MagicChatbox.Tests.TestDoubles;

// Consent as a set a test can arrange directly. Defaults to approving nothing, because a guard that
// is only ever exercised against an approving stub is a guard nobody has watched refuse.
public sealed class StubConsentService : IPrivacyConsentService
{
    private readonly Dictionary<PrivacyHook, ConsentState> _states = new();

    public static StubConsentService ApprovingAll()
    {
        var service = new StubConsentService();

        foreach (PrivacyHook hook in Enum.GetValues<PrivacyHook>())
            service.Approve(hook);

        return service;
    }

    public bool IsApproved(PrivacyHook hook) => GetState(hook) == ConsentState.Approved;

    public ConsentState GetState(PrivacyHook hook)
        => _states.TryGetValue(hook, out ConsentState state) ? state : ConsentState.Unknown;

    public void Approve(PrivacyHook hook) => Set(hook, ConsentState.Approved);

    public void Deny(PrivacyHook hook) => Set(hook, ConsentState.Denied);

    public void Reset(PrivacyHook hook) => Set(hook, ConsentState.Unknown);

    public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
        => hooks.Where(h => GetState(h) == ConsentState.Unknown).ToList();

    public event EventHandler<ConsentChangedEventArgs>? ConsentChanged;

    private void Set(PrivacyHook hook, ConsentState state)
    {
        _states[hook] = state;
        ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, state));
    }
}
