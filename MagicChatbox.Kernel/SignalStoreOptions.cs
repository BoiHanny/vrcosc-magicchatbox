using System.Collections.Frozen;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// The knobs a <see cref="SignalStore"/> has. Every default is the production value.
/// </summary>
/// <remarks>
/// These exist so a test can build a store with four stripes and a cell cap of two and assert the
/// behaviour deterministically, not so an operator can tune the kernel. Nothing here is settings.
/// </remarks>
public sealed record SignalStoreOptions
{
    /// <summary>
    /// 64 per-key stripes, kept verbatim from v2, where it is measured at the required rate in this
    /// codebase today. Must be a power of two: the stripe index is <c>Hash &amp; (count - 1)</c>.
    /// </summary>
    public int StripeCount { get; init; } = 64;

    /// <summary>
    /// The monotonic clock. <c>TimeProvider.System.GetTimestamp()</c> is <c>Stopwatch.GetTimestamp()</c>;
    /// staleness measured against a wall clock breaks across a DST shift and across an NTP correction,
    /// both of which happen to real users.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>
    /// Per-namespace cell caps (D10). Cells are permanent until eviction, so an unbounded key generator
    /// is an unbounded memory leak — and wildcard parameter families such as
    /// <c>avatar.param.osc_bank_&lt;n&gt;_&lt;m&gt;</c> are a real thing avatars do.
    /// </summary>
    /// <remarks>
    /// The cap <b>rejects</b> with a named reason rather than evicting. Eviction would silently delete a
    /// fact somebody is rendering, which is the failure mode this whole design exists to remove.
    /// </remarks>
    public FrozenDictionary<SignalNamespace, int> NamespaceCellCaps { get; init; } = DefaultCellCaps;

    /// <summary>
    /// How long a key must wait before it can put another rejection on the occurrence tape.
    /// </summary>
    /// <remarks>
    /// A malfunctioning avatar sending NaN on a face-tracking parameter at 60 Hz would otherwise flood
    /// the very tape the design keeps the firehose off. The counter still moves on every rejection;
    /// only the narration is throttled.
    /// </remarks>
    public int RejectionReportIntervalMs { get; init; } = 1000;

    /// <summary>The meter name, so a test can listen to its own store without hearing the process's.</summary>
    public string MeterName { get; init; } = "MagicChatbox.Kernel";

    /// <summary>The production caps. A fat avatar is roughly 400 parameters.</summary>
    public static FrozenDictionary<SignalNamespace, int> DefaultCellCaps { get; } =
        new Dictionary<SignalNamespace, int>
        {
            [SignalNamespace.Avatar] = 2_048,
            [SignalNamespace.Input] = 512,
            [SignalNamespace.System] = 256,
            [SignalNamespace.Module] = 4_096,
            [SignalNamespace.App] = 256,

            // VRChat's non-avatar subsystems. 128 against a measured ceiling of ~80: /usercamera
            // advertises 37 leaves, /dolly 5, and the tracked poses 18 — and unlike avatar parameters
            // this set is fixed by VRChat rather than by whatever an avatar author invented, so it grows
            // only when VRChat ships a new subsystem.
            [SignalNamespace.Vrc] = 128,
        }.ToFrozenDictionary();
}
