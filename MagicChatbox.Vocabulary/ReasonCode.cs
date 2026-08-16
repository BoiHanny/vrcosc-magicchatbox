namespace MagicChatbox.Vocabulary;

/// <summary>
/// Why something is the way it is — the machine-readable half of every rejection, every unavailable
/// signal, and every blocked egress attempt.
/// </summary>
/// <remarks>
/// A closed enum rather than a string. "Spotify not connected" is the product's commonest user-visible
/// state, and in v2 it was a magic string invented independently by each provider
/// (<c>"Nothing playing"</c>, <c>"Idle"</c>, <c>"No media session"</c> — MediaPilotRuntime.cs:1568-1600).
/// One vocabulary means the UI can localise it, the composer can suppress on it, and a test can assert it.
/// <para>
/// Numeric values are stable and gaps are never reused: a value that reaches a log or a saved document
/// must keep its meaning. Append new members; never renumber.
/// </para>
/// </remarks>
public enum ReasonCode : ushort
{
    /// <summary>No reason recorded. Only valid where a reason is genuinely absent, never as a default.</summary>
    None = 0,

    /// <summary>Succeeded.</summary>
    Ok = 1,

    // ── Availability (why a signal has no meaningful value) ─────────────────────────────────────
    /// <summary>The source exists but has never produced a value.</summary>
    NeverObserved = 10,

    /// <summary>The source is configured but not connected — the "Spotify not connected" case.</summary>
    NotConnected = 11,

    /// <summary>A value exists but is older than its descriptor's StaleAfterMs.</summary>
    Stale = 12,

    /// <summary>The source is connected but reports it has nothing to give (paused, idle, no session).</summary>
    NoContent = 13,

    /// <summary>The source is disabled by the user.</summary>
    SourceDisabled = 14,

    /// <summary>The source faulted. Detail carries the diagnostic; the last good value is retained.</summary>
    SourceFaulted = 15,

    /// <summary>
    /// The source works and takes too long doing it, so the host has filed a strike against it.
    /// </summary>
    /// <remarks>
    /// <b>Its own code rather than <see cref="SourceFaulted"/>, because nothing threw.</b> A module whose
    /// tick blocks on a file read produces no exception at all — it produces a stalled 33 ms host sweep,
    /// which is the failure §3.13 of the revision plan is about — and filing that under a code whose own
    /// sentence is "the source threw" would put a line on somebody's card that sends them looking for a
    /// stack trace that does not exist. The detail beside it carries the measurement.
    /// </remarks>
    SourceTooSlow = 16,

    // ── Write rejections ────────────────────────────────────────────────────────────────────────
    /// <summary>No descriptor is registered for this key.</summary>
    UnknownKey = 30,

    /// <summary>The value's kind does not match the descriptor and no legal conversion exists.</summary>
    KindMismatch = 31,

    /// <summary>The caller's grant does not cover this key.</summary>
    NotGranted = 32,

    /// <summary>The caller's scope was revoked.</summary>
    ScopeRevoked = 33,

    /// <summary>
    /// The key is observation-only: it reflects external truth and may not be set directly.
    /// Writing an avatar parameter goes through egress, not through the store.
    /// </summary>
    ObservedOnly = 34,

    /// <summary>
    /// A float arrived as NaN or Infinity. Rejected at the boundary: epsilon dedupe can never
    /// collapse NaN, so an accepted NaN publishes on every observation and reaches the chatbox (D4).
    /// </summary>
    NonFiniteValue = 35,

    /// <summary>A Text descriptor was registered on the observation path, which is forbidden (D7).</summary>
    TextOnObservePath = 36,

    /// <summary>A Text value exceeded the 256-byte cap.</summary>
    TextTooLong = 37,

    /// <summary>The namespace has reached its cell cap; a new key would grow memory unboundedly (D10).</summary>
    NamespaceCellCapReached = 38,

    // ── Egress ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>The current world is on the user's mute list. Nothing is sent to the chatbox there.</summary>
    EgressWorldMuted = 50,

    /// <summary>The profanity filter blocked the text. Detail carries the matched term.</summary>
    EgressProfanityBlocked = 51,

    /// <summary>The message exceeded VRChat's 144-character chatbox limit. Characters, not bytes.</summary>
    EgressBudgetExceeded = 52,

    /// <summary>The courtesy cadence has not elapsed. Not VRChat's enforced limit — see §12.7.</summary>
    RateLimited = 53,

    /// <summary>VRChat is not reachable: no OSCQuery peer and no manual endpoint configured.</summary>
    EgressNoEndpoint = 54,

    /// <summary>The write was dispatched but no echo confirmed it within the timeout.</summary>
    EgressAckTimeout = 55,

    /// <summary>The value is not representable on the wire for this surface.</summary>
    EgressUnsupportedValue = 56,

    // ── Storage ─────────────────────────────────────────────────────────────────────────────────
    /// <summary>The durable store could not be opened. Nothing is being saved.</summary>
    StoreUnavailable = 70,

    /// <summary>The store opened but cannot be written to — a read-only file, a full disk, a failed migration.</summary>
    StoreReadOnly = 71,

    /// <summary>The file failed its integrity check and was moved aside. A fresh one is in use.</summary>
    StoreCorrupt = 72,

    /// <summary>
    /// The file's <c>schema_version</c> is higher than this build knows. Opened read-only rather than
    /// migrated down.
    /// </summary>
    /// <remarks>
    /// The <i>table</i> shape axis, and never the payload one. A document whose <c>shape_version</c> is
    /// ahead of its reader is <see cref="PayloadInvalid"/>; the two version numbers are spelled
    /// differently in the schema precisely so they cannot be confused, and reusing this code for a
    /// payload would undo that.
    /// </remarks>
    SchemaTooNew = 73,

    /// <summary>The caller's expected revision did not match the stored one. Nothing was written.</summary>
    RevisionConflict = 74,

    /// <summary>No document exists for that (kind, id).</summary>
    DocumentNotFound = 75,

    /// <summary>The stored payload could not be parsed by this build. The row is preserved as a broken document.</summary>
    PayloadInvalid = 76,

    /// <summary>
    /// Something was over its size cap and was not stored, sent or read.
    /// </summary>
    /// <remarks>
    /// Per-document at first and no longer only that: a brokered response bigger than the cap the host
    /// reads it under (<c>ModuleHttpService.MaxResponseBytes</c>) answers this too. One code for both,
    /// because from the caller's side they are one fact — the thing did not fit and retrying it unchanged
    /// will not help — and splitting them would be two words for one instruction.
    /// </remarks>
    PayloadTooLarge = 77,

    /// <summary>DPAPI refused the blob — written by a different Windows user or machine. Treated as absent.</summary>
    SecretUnprotectFailed = 78,

    /// <summary>
    /// A module asked to keep one more row than its budget allows. Nothing was written and the rows it
    /// already has are untouched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not <see cref="PayloadTooLarge"/>, and the two are one keystroke apart in meaning.</b> That one
    /// says <i>this value is too big for a row</i> and is fixed by writing less; this one says <i>you
    /// have as many rows as you get</i> and is fixed by removing one. An author told the wrong one of
    /// those spends an afternoon shrinking a payload that was never the problem.
    /// </para>
    /// <para>
    /// The budget is <c>IModuleStore.MaxRows</c> and the argument for its size is there: the ceiling is
    /// the Data screen staying a list a person can read, not the disk.
    /// </para>
    /// </remarks>
    ModuleStoreFull = 79,

    // ── VRChat's output log ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The log folder is there and holds no <c>output_log_*.txt</c>, which is what a client launched
    /// with logging disabled looks like. Nothing can be told about where the player is.
    /// </summary>
    /// <remarks>
    /// Distinct from the folder being absent entirely, which is not a failure at all: a machine that has
    /// never run VRChat leaves every world key at <see cref="NeverObserved"/>, and saying "logging is
    /// turned off" about a machine with no VRChat on it would be a diagnosis of the wrong problem.
    /// </remarks>
    LogUnavailable = 90,

    /// <summary>
    /// The newest log has not grown for minutes while VRChat is still answering on the wire. The last
    /// world is retained and marked <c>Stale</c> — it is probably still right, and it is certainly not
    /// wrong enough to throw away.
    /// </summary>
    LogStalled = 91,

    /// <summary>
    /// Lines are being read and none of them matches any pattern the log grammar knows, which is what a
    /// VRChat update that renamed its log lines looks like from here.
    /// </summary>
    /// <remarks>
    /// The format is undocumented and unversioned, so this is a state the product is expected to reach
    /// eventually rather than a bug. Everything downstream must treat it as "we do not know where you
    /// are" — <b>an unknown world is not an unmuted world</b> — which is why the affected keys go
    /// <c>Unavailable</c> and never to a guessed value.
    /// </remarks>
    LogFormatUnknown = 92,

    // ── The avatar swap ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The avatar was still streaming its parameters at their defaults, so an automatic reaction to one
    /// of them was held rather than run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <see cref="Stale"/> and not <see cref="NeverObserved"/>, which are the two nearby readings and
    /// are both about a cell nobody can believe. The values behind this one are live, current and
    /// genuinely changing — VRChat is telling the truth that <c>Blush</c> is 0 — and they are simply not
    /// yet what the wearer meant. A cell-level reason would have said the parameter was unreadable, which
    /// would send somebody looking at their heart-rate strap.
    /// </para>
    /// <para>
    /// <b>It is only ever a reason for holding an <i>automatic</i> reaction.</b> Nothing a person asked
    /// for directly — Panic, a manual send, Run now — may carry it, because a player pressing a button
    /// mid-swap must be obeyed.
    /// </para>
    /// </remarks>
    AvatarSettling = 100,

    // ── The room ────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The value is readable and was withheld anyway, because the instance is one anybody can walk into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <see cref="NotGranted"/>, which is the nearest-looking member and is about authorization: a
    /// grant is a property of the caller and is the same in every world. This is a property of the
    /// <i>room</i>, it changes when the player travels, and the same reader gets the same cell back the
    /// moment they join a friends-only instance. A caller told <see cref="NotGranted"/> would go looking
    /// for a permission to widen and there is none.
    /// </para>
    /// <para>
    /// Not <see cref="NoContent"/> either: the source has plenty to say. Reporting an absence would send
    /// somebody to the Sources screen to fix a module that is working perfectly.
    /// </para>
    /// </remarks>
    Curtained = 110,

    // ── The network boundary ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The destination is not on this build's allow-list, so the request was never made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A refusal by this application, not a failure of the network</b>, and the distinction is the
    /// whole reason it is a separate member from <see cref="NetworkUnreachable"/>. Somebody reading the
    /// Diagnostics log after a feature went quiet needs to know whether their firewall ate the request or
    /// whether MagicChatbox declined to send it — the first is theirs to fix and the second is a bug or a
    /// missing entry in <c>NetworkAllowList</c>, and no amount of retrying will change the second.
    /// </para>
    /// <para>
    /// Raised before the request leaves the process. It is also what a non-HTTPS destination gets: the
    /// allow-list answers one question — <i>may this exact request go out</i> — and splitting "wrong
    /// scheme" into its own code would imply the host would otherwise have been fine, which is not a
    /// thing this build ever concludes.
    /// </para>
    /// </remarks>
    NetworkHostNotAllowed = 120,

    /// <summary>
    /// The request was allowed and got no answer: DNS, a socket, a timeout, or the machine being offline.
    /// </summary>
    /// <remarks>
    /// The ordinary state of a laptop in a bag, and therefore never an error the user is asked to act on.
    /// Whatever asked for the data keeps whatever it already had — see <c>CommunityWorldList</c>, whose
    /// entire failure posture is that a fetch which does not answer must leave the last-known-good list
    /// exactly where it was.
    /// </remarks>
    NetworkUnreachable = 121,

    /// <summary>
    /// The destination answered, and the answer was not a success. Detail carries the status code.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="NetworkUnreachable"/> because the two mean opposite things about the
    /// destination: this one is reachable and said no, which is what a moved file, a rate limit or a
    /// deleted repository looks like — a thing that stays broken until somebody changes a URL, rather
    /// than a thing that fixes itself when the train comes out of the tunnel.
    /// </remarks>
    NetworkRejected = 122,

    /// <summary>
    /// A brokered caller has used its allowance of requests for this window. Nothing was sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A refusal by this application and not by the destination</b>, which is what keeps it apart from
    /// <see cref="NetworkRejected"/>: a service answering <c>429</c> is a fact about that service and is
    /// fixed by asking it less often, whereas this is MagicChatbox declining to ask at all. Retrying
    /// changes nothing until the window moves.
    /// </para>
    /// <para>
    /// Also not <see cref="RateLimited"/>, which is the chatbox's courtesy cadence and means <i>this will
    /// go out shortly</i>. Nothing here is queued: the request is refused and the caller decides whether
    /// there is still a point in it a minute from now.
    /// </para>
    /// <para>
    /// The budget it names is <c>ModuleHttpService.RequestBudget</c>, and the size of that number is
    /// argued there — the binding constraint is the 256-entry occurrence ring, because the brokered
    /// design writes one line on it per request so the Activity screen can attribute them.
    /// </para>
    /// </remarks>
    NetworkBudgetSpent = 123,
}
