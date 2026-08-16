using System.Text;

namespace MagicChatbox.Vocabulary;

/// <summary>
/// One fact's value: a closed four-case union over bool, long, double and string.
/// </summary>
/// <remarks>
/// A struct union rather than <c>object?</c>. v2 boxed every reading on a 2,700/sec path and then
/// spent 452 lines of codec and comparer un-boxing it again; four cases in sixteen bytes deletes both.
/// <para>
/// <b>D3 — equality is bitwise, and that is not negotiable.</b> An earlier draft compared floats with a
/// 1e-6 epsilon. That is a defect twice over: epsilon equality is not transitive, so every
/// <c>Dictionary</c>, <c>HashSet</c> and <c>Distinct()</c> keyed on a value silently corrupts; and
/// epsilon-equal values hash differently, which breaks the Equals/GetHashCode contract outright.
/// Epsilon comparison is a <i>dedupe policy</i>, and it lives in <see cref="NearlyEquals"/>, internal,
/// with exactly one intended caller.
/// </para>
/// <para>
/// <c>default(SignalValue)</c> is <see cref="SignalKind.Bool"/> false. That is deliberate: the default
/// of a value type has to mean something, and "false" is the only case with a defensible zero.
/// </para>
/// </remarks>
public readonly struct SignalValue : IEquatable<SignalValue>
{
    /// <summary>
    /// A byte cap, because every wire format downstream of a cell counts bytes: OSC strings, the SQLite
    /// payload, the OSCQuery tree. Counting characters here would let a caller past a real byte limit
    /// fourfold and only fail at the wire, far from the mistake.
    /// </summary>
    /// <remarks>
    /// <b>This is not VRChat's chatbox budget, and it must not be read as one.</b> That budget is 144
    /// <i>characters</i> (<c>VrcChatboxLimits</c>), and a character there is a grapheme cluster with no
    /// byte bound — a ZWJ family emoji is one character and twenty-five bytes. So a legal 144-character
    /// chatbox line can exceed 256 bytes and cannot be carried as a <c>Text</c> value at all.
    /// Composed chatbox output belongs in the document tier for exactly that reason; this cap is about
    /// keeping one cell small, not about what VRChat will display.
    /// </remarks>
    public const int MaxTextUtf8Bytes = 256;

    /// <summary>The largest double that truncates into a <see cref="long"/>: 2^63 is one past the top.</summary>
    private const double TruncationUpperBound = 9223372036854775808.0;

    private const double TruncationLowerBound = -9223372036854775808.0;

    private readonly long _bits;
    private readonly string? _text;

    private SignalValue(SignalKind kind, long bits, string? text)
    {
        Kind = kind;
        _bits = bits;
        _text = text;
    }

    /// <summary>Which of the four cases this is.</summary>
    public SignalKind Kind { get; }

    public static bool operator ==(SignalValue left, SignalValue right) => left.Equals(right);

    public static bool operator !=(SignalValue left, SignalValue right) => !left.Equals(right);

    public static SignalValue Bool(bool value) => new(SignalKind.Bool, value ? 1L : 0L, null);

    public static SignalValue Int(long value) => new(SignalKind.Int, value, null);

    /// <summary>
    /// Builds a float, non-finite values included.
    /// </summary>
    /// <remarks>
    /// NaN and Infinity are representable here and rejected at the store (D4). This factory does not
    /// throw, because the OSC decoder must be able to build the value it actually received before
    /// anything can reject it with a named reason; a throwing factory would turn a malfunctioning
    /// avatar into an exception on the ingress path.
    /// </remarks>
    public static SignalValue Float(double value) =>
        new(SignalKind.Float, BitConverter.DoubleToInt64Bits(value), null);

    /// <summary>
    /// Builds text, ordinal and case-sensitive. Throws past <see cref="MaxTextUtf8Bytes"/>.
    /// </summary>
    /// <remarks>
    /// This one throws where <see cref="Float(double)"/> does not, and the asymmetry is the point.
    /// A non-finite float is something the outside world <i>sent us</i> — an expected malfunction that
    /// must survive as a value long enough to be rejected by name. An over-long string is something a
    /// caller <i>composed</i>: it is a bug at the call site, and 257 bytes has no honest
    /// representation to carry forward. Boundaries that accept untrusted text — the module SDK, the
    /// HTTP surface — call <see cref="TryText"/> and report <c>ReasonCode.TextTooLong</c> instead of
    /// catching.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> exceeds the byte cap.</exception>
    public static SignalValue Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetByteCount(value);
        if (bytes > MaxTextUtf8Bytes)
        {
            throw new ArgumentException(
                $"Text is {bytes} UTF-8 bytes; the cap is {MaxTextUtf8Bytes}.", nameof(value));
        }

        return new SignalValue(SignalKind.Text, 0L, value);
    }

    /// <summary>The non-throwing half of <see cref="Text(string)"/>, for untrusted input.</summary>
    public static bool TryText(string? value, out SignalValue result)
    {
        if (!IsTextWithinLimit(value))
        {
            result = default;
            return false;
        }

        result = new SignalValue(SignalKind.Text, 0L, value);
        return true;
    }

    /// <summary>False for null and for anything past the byte cap.</summary>
    public static bool IsTextWithinLimit(string? value) =>
        value is not null && Encoding.UTF8.GetByteCount(value) <= MaxTextUtf8Bytes;

    /// <summary>The cap's unit, exposed so callers measure what the cap measures.</summary>
    public static int Utf8ByteCount(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetByteCount(value);
    }

    public bool AsBool() => Kind == SignalKind.Bool
        ? _bits != 0L
        : throw WrongKind(SignalKind.Bool);

    public long AsInt() => Kind == SignalKind.Int
        ? _bits
        : throw WrongKind(SignalKind.Int);

    public double AsFloat() => Kind == SignalKind.Float
        ? BitConverter.Int64BitsToDouble(_bits)
        : throw WrongKind(SignalKind.Float);

    public string AsText() => Kind == SignalKind.Text
        ? _text!
        : throw WrongKind(SignalKind.Text);

    /// <summary>
    /// False only for a non-finite float. The store calls this at the boundary so a NaN never reaches
    /// dedupe (D4) — epsilon can never collapse NaN, so an accepted one publishes on every single
    /// observation and renders as <c>NaN°C</c> in the chatbox.
    /// </summary>
    public bool IsFinite() => Kind != SignalKind.Float || double.IsFinite(BitConverter.Int64BitsToDouble(_bits));

    /// <summary>
    /// The conversion matrix of §5.3, total and allocation-free.
    /// </summary>
    /// <remarks>
    /// Two rules earn their explanation. <b>Float to Int truncates toward zero and does not round</b> —
    /// v2 did the same, but only implicitly via a C# cast, so a future refactor to <c>Math.Round</c>
    /// would have silently changed every avatar parameter that crosses an integer boundary. <b>Text
    /// converts to nothing in either direction</b> — the OSC wire structurally cannot carry text on the
    /// hot path, and string parsing is a config-boundary concern that belongs in <c>Vrc</c> next to the
    /// JSON it comes from, where a parse failure is an expected outcome with a reason code. Admitting
    /// it here would drag culture handling and <c>Convert.To*</c> fallbacks into the foundation.
    /// <para>A non-finite float converts to nothing at all, itself included, so it cannot launder
    /// itself through a Float-to-Float conversion.</para>
    /// </remarks>
    public bool TryConvertTo(SignalKind target, out SignalValue converted)
    {
        converted = default;

        if (!IsFinite())
        {
            return false;
        }

        switch (Kind)
        {
            case SignalKind.Bool:
                switch (target)
                {
                    case SignalKind.Bool:
                        converted = this;
                        return true;
                    case SignalKind.Int:
                        converted = Int(_bits != 0L ? 1L : 0L);
                        return true;
                    case SignalKind.Float:
                        converted = Float(_bits != 0L ? 1.0 : 0.0);
                        return true;
                    default:
                        return false;
                }

            case SignalKind.Int:
                switch (target)
                {
                    case SignalKind.Bool:
                        converted = Bool(_bits != 0L);
                        return true;
                    case SignalKind.Int:
                        converted = this;
                        return true;
                    case SignalKind.Float:
                        // "Widen (exact)", read literally: past 2^53 a long is not exactly
                        // representable, and dropping low bits silently is the kind of quiet lie this
                        // matrix exists to forbid. Unreachable from OSC, whose ints are 32-bit.
                        // The bounds check comes first because the cast back saturates rather than
                        // wrapping: (long)(double)long.MaxValue is long.MaxValue, so a naive
                        // round-trip would declare the widening exact when it lost the low bits.
                        var widened = (double)_bits;
                        if (widened < TruncationLowerBound
                            || widened >= TruncationUpperBound
                            || (long)widened != _bits)
                        {
                            return false;
                        }

                        converted = Float(widened);
                        return true;
                    default:
                        return false;
                }

            case SignalKind.Float:
                var value = BitConverter.Int64BitsToDouble(_bits);
                switch (target)
                {
                    case SignalKind.Bool:
                        converted = Bool(value != 0.0);
                        return true;
                    case SignalKind.Int:
                        // An unchecked cast saturates to long.MinValue/MaxValue on overflow, which
                        // asserts a number nobody sent. Out of range is a failed conversion instead.
                        if (value < TruncationLowerBound || value >= TruncationUpperBound)
                        {
                            return false;
                        }

                        converted = Int((long)value);
                        return true;
                    case SignalKind.Float:
                        converted = this;
                        return true;
                    default:
                        return false;
                }

            case SignalKind.Text:
                if (target != SignalKind.Text)
                {
                    return false;
                }

                converted = this;
                return true;

            default:
                return false;
        }
    }

    public bool Equals(SignalValue other) =>
        Kind == other.Kind
        && _bits == other._bits
        && string.Equals(_text, other._text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SignalValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        (byte)Kind,
        _bits,
        _text is null ? 0 : string.GetHashCode(_text, StringComparison.Ordinal));

    public override string ToString() => Kind switch
    {
        SignalKind.Bool => _bits != 0L ? "true" : "false",
        SignalKind.Int => _bits.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SignalKind.Float => BitConverter.Int64BitsToDouble(_bits)
            .ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => _text ?? string.Empty,
    };

    /// <summary>
    /// The store's dedupe comparison, and the only place epsilon appears.
    /// </summary>
    /// <remarks>
    /// Not an equality relation: deliberately not transitive, deliberately not consistent with
    /// <see cref="GetHashCode"/>, and deliberately not named <c>Equals</c>. Its one legitimate caller
    /// is the change test inside the store's stripe lock, where "close enough to not be worth
    /// publishing" is the actual question. Anything that puts a value in a hash container must use
    /// <see cref="Equals(SignalValue)"/>.
    /// <para>
    /// Note that <c>NearlyEquals(NaN, NaN)</c> is false — epsilon can never collapse a NaN, which is
    /// precisely why the store rejects non-finite readings before it ever gets here (D4).
    /// </para>
    /// </remarks>
    internal static bool NearlyEquals(in SignalValue a, in SignalValue b, double epsilon = 1e-6)
    {
        if (a.Kind != b.Kind)
        {
            return false;
        }

        return a.Kind == SignalKind.Float
            ? Math.Abs(a.AsFloat() - b.AsFloat()) < epsilon
            : a.Equals(b);
    }

    private InvalidOperationException WrongKind(SignalKind requested) =>
        new($"This value is {Kind}, not {requested}.");
}
