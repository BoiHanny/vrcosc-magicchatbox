using System.Diagnostics.Metrics;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// The kernel's instruments, wired from day one rather than retrofitted.
/// </summary>
/// <remarks>
/// Two of these are the whole point. <c>kernel.rejections</c> tagged by reason makes a NaN storm or a
/// misdeclared Text key <b>announce itself by name within a second</b>, instead of surfacing a week
/// later as missing ledger rows. <c>kernel.intern_miss_rate</c> makes the 4,096-entry intern cap
/// degrading into per-packet allocation visible, because its only other symptom is GC pressure
/// somebody eventually profiles a long way from the cause.
/// <para>
/// Counters are also exposed as plain properties on <see cref="SignalStore.Counters"/>, because a test
/// asserting on a <c>MeterListener</c> is a test asserting on plumbing.
/// </para>
/// </remarks>
public sealed class KernelMetrics : IDisposable
{
    private readonly Meter _meter;

    internal KernelMetrics(string meterName, SignalStore store, KernelHealth health)
    {
        _meter = new Meter(meterName);

        Mutations = _meter.CreateCounter<long>("kernel.mutations", "writes", "Writes by status.");
        Rejections = _meter.CreateCounter<long>("kernel.rejections", "writes", "Rejected writes by reason.");
        StalenessFlips = _meter.CreateCounter<long>("kernel.staleness_flips", "cells", "Cells flipped Live to Stale.");
        NonFiniteRejected = _meter.CreateCounter<long>(
            "kernel.nonfinite_rejected", "readings", "Non-finite float readings rejected at the boundary (D4).");

        _meter.CreateObservableGauge(
            "kernel.cells_by_namespace", store.ObserveCellsByNamespace, "cells", "Live cell count per namespace (D10).");

        _meter.CreateObservableGauge(
            "kernel.cells_by_availability", store.ObserveCellsByAvailability, "cells", "Cell count per availability.");

        _meter.CreateObservableGauge(
            "kernel.intern_miss_rate",
            static () => SignalKeyInternTable.Shared.Stats.MissRate,
            "ratio",
            "Intern misses over attempts. Alarm above 0.01 sustained (D10).");

        _meter.CreateObservableGauge(
            "kernel.sink_ejections", () => (long)health.Ejections.Length, "sinks", "Sinks removed from fan-out (D12).");
    }

    internal Counter<long> Mutations { get; }

    internal Counter<long> Rejections { get; }

    internal Counter<long> StalenessFlips { get; }

    internal Counter<long> NonFiniteRejected { get; }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
