namespace MagicChatbox.Kernel;

/// <summary>
/// A point-in-time reading of everything the store counts.
/// </summary>
/// <remarks>
/// The same numbers the meter publishes, in a shape a test can assert on directly. A test that has to
/// stand up a <c>MeterListener</c> to find out whether a NaN was rejected is a test about plumbing.
/// </remarks>
/// <param name="Accepted">Writes that changed a cell.</param>
/// <param name="NoChange">Writes that were legal and indistinguishable from the current value.</param>
/// <param name="Rejected">Writes refused by policy or by a boundary check.</param>
/// <param name="ObservationsAccepted">Readings from the observe path that changed a cell.</param>
/// <param name="ObservationsRejected">Readings the observe path refused.</param>
/// <param name="NonFiniteRejected">D4: NaN and Infinity refused at the boundary. Alarm on any non-zero value.</param>
/// <param name="TextOnObservePathRejected">D7: Text descriptors met on the hot path. Alarm on any non-zero value.</param>
/// <param name="NamespaceCapRejected">D10: new cells refused because a namespace is full.</param>
/// <param name="KindMismatchRejected">Values the conversion matrix refused to coerce.</param>
/// <param name="UnknownKeyRejected">Writes to keys nobody has declared.</param>
/// <param name="StalenessFlips">Cells the sweep moved from Live to Stale.</param>
/// <param name="CellsRemoved">Cells evicted by <see cref="SignalStore.RemoveMatching"/>.</param>
public readonly record struct SignalStoreCounters(
    long Accepted,
    long NoChange,
    long Rejected,
    long ObservationsAccepted,
    long ObservationsRejected,
    long NonFiniteRejected,
    long TextOnObservePathRejected,
    long NamespaceCapRejected,
    long KindMismatchRejected,
    long UnknownKeyRejected,
    long StalenessFlips,
    long CellsRemoved);
