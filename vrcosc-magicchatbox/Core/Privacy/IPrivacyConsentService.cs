using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Privacy;

public interface IPrivacyConsentService
{
    bool IsApproved(PrivacyHook hook);
    ConsentState GetState(PrivacyHook hook);
    void Approve(PrivacyHook hook);
    void Deny(PrivacyHook hook);
    void Reset(PrivacyHook hook);
    IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks);

    event EventHandler<ConsentChangedEventArgs> ConsentChanged;
}

public sealed class ConsentChangedEventArgs : EventArgs
{
    public PrivacyHook Hook { get; }
    public ConsentState NewState { get; }

    public ConsentChangedEventArgs(PrivacyHook hook, ConsentState newState)
    {
        Hook = hook;
        NewState = newState;
    }
}
