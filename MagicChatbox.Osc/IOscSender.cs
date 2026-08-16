namespace MagicChatbox.Osc;

/// <summary>
/// Writes a raw OSC message to VRChat. <b>Deliberately <c>internal</c>.</b>
/// </summary>
/// <remarks>
/// <para><b>D5 — this being <c>internal</c> is a safety control, not a style choice.</b></para>
/// <para>
/// An earlier v3 structure document made this interface <c>public</c> and gave <c>Core</c> a direct
/// reference to this assembly. That combination lets any of <c>Core</c>, <c>Api</c> or <c>Shell</c>
/// construct a message for <c>/chatbox/input</c> and write it past the character budget, the cadence gate and
/// the world blacklist — which is exactly the hole audit F-143 closed in v2, and exactly the hole
/// <c>ModuleRuntime.cs:604</c> still has open today. v2 shipped three egress doors and two checks and
/// produced three holes of one class; attaching the safety to each caller has now failed twice.
/// </para>
/// <para>
/// So the send side is reachable only from <c>MagicChatbox.Vrc</c>, which owns the gates, and the
/// compiler enforces it for every other assembly. <c>internal</c> alone is not sufficient — both v2
/// holes were same-assembly-reachable — which is why the sender also lives in an assembly that contains
/// nothing else worth referencing, and why an architecture test asserts no other assembly names it.
/// </para>
/// <para>
/// If you are reading this because you want to send something MagicChatbox cannot currently send: add a
/// method to <c>IVrcEgress</c>. That is a protocol change and is reviewed as one. Do not widen this.
/// </para>
/// </remarks>
internal interface IOscSender
{
    /// <summary>Sends one message. Returns false when no endpoint is configured.</summary>
    ValueTask<bool> SendAsync(OscMessage message, CancellationToken cancellationToken);
}
