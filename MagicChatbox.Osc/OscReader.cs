using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace MagicChatbox.Osc;

/// <summary>One decoded OSC argument, still pointing into the received datagram.</summary>
/// <remarks>
/// <para>
/// A <c>ref struct</c> because the whole point is that decoding costs nothing. Local ingress runs at
/// hundreds to thousands of messages per second under face tracking (§12.1, P9 — the widely-quoted 10 Hz
/// is VRChat's cross-player *network* sync, not this path), so a per-argument box or a per-string
/// allocation is steady garbage measured in megabytes per minute.
/// </para>
/// <para>
/// The discriminator is the wire's own type tag rather than <see cref="OscArgKind"/>, because the wire
/// carries tags we can size but do not support, and collapsing those into a supported kind would be a
/// lie. <see cref="IsSupported"/> separates the two. Per P1 the type tag is authoritative for decoding —
/// OSCQuery is never consulted here.
/// </para>
/// </remarks>
public readonly ref struct OscValue
{
    private readonly ReadOnlySpan<byte> _utf8;
    private readonly int _bits;

    private OscValue(char typeTag, int bits, ReadOnlySpan<byte> utf8)
    {
        TypeTag = typeTag;
        _bits = bits;
        _utf8 = utf8;
    }

    /// <summary>The OSC 1.0 type tag this argument arrived with.</summary>
    public char TypeTag { get; }

    /// <summary>True for the five tags VRChat actually sends: <c>T</c>, <c>F</c>, <c>i</c>, <c>f</c>, <c>s</c>.</summary>
    public bool IsSupported => TypeTag is 'T' or 'F' or 'i' or 'f' or 's';

    /// <summary>The encoder-side kind. Only meaningful when <see cref="IsSupported"/>.</summary>
    /// <exception cref="InvalidOperationException">The tag is one we can size but not represent.</exception>
    public OscArgKind Kind => TypeTag switch
    {
        'T' or 'F' => OscArgKind.Bool,
        'i' => OscArgKind.Int32,
        'f' => OscArgKind.Float32,
        's' => OscArgKind.String,
        _ => throw new InvalidOperationException($"OSC type tag '{TypeTag}' has no {nameof(OscArgKind)}."),
    };

    internal static OscValue Bool(bool value) => new(value ? 'T' : 'F', value ? 1 : 0, default);

    internal static OscValue Int32(int value) => new('i', value, default);

    internal static OscValue Float32(float value) => new('f', BitConverter.SingleToInt32Bits(value), default);

    internal static OscValue String(ReadOnlySpan<byte> utf8) => new('s', 0, utf8);

    /// <summary>A tag we could size and skip, but cannot represent. The payload is deliberately not exposed.</summary>
    internal static OscValue Unsupported(char typeTag) => new(typeTag, 0, default);

    /// <exception cref="InvalidOperationException">This argument is not a boolean.</exception>
    public bool AsBool() => TypeTag is 'T' or 'F'
        ? _bits != 0
        : throw new InvalidOperationException($"OSC argument '{TypeTag}' is not a boolean.");

    /// <exception cref="InvalidOperationException">This argument is not an int32.</exception>
    public int AsInt32() => TypeTag == 'i'
        ? _bits
        : throw new InvalidOperationException($"OSC argument '{TypeTag}' is not an int32.");

    /// <exception cref="InvalidOperationException">This argument is not a float32.</exception>
    public float AsFloat32() => TypeTag == 'f'
        ? BitConverter.Int32BitsToSingle(_bits)
        : throw new InvalidOperationException($"OSC argument '{TypeTag}' is not a float32.");

    /// <summary>The raw UTF-8 bytes of a string argument, without the terminator or padding.</summary>
    /// <remarks>Prefer this over <see cref="AsString"/>: interning from the span skips the allocation.</remarks>
    /// <exception cref="InvalidOperationException">This argument is not a string.</exception>
    public ReadOnlySpan<byte> AsUtf8() => TypeTag == 's'
        ? _utf8
        : throw new InvalidOperationException($"OSC argument '{TypeTag}' is not a string.");

    /// <summary>Materializes a string argument. Allocates — the only decode step that does.</summary>
    public string AsString() => Encoding.UTF8.GetString(AsUtf8());

    /// <summary>Materializes this value as an encoder-side <see cref="OscArg"/>.</summary>
    /// <remarks>
    /// The seam where the zero-allocation decode path ends. It exists for round-trip tests and for the
    /// buffered stream adapter; the hot path reads <see cref="OscValue"/> directly and never calls this.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The tag is one we can size but not represent.</exception>
    public OscArg ToArg() => Kind switch
    {
        OscArgKind.Bool => OscArg.Bool(AsBool()),
        OscArgKind.Int32 => OscArg.Int32(AsInt32()),
        OscArgKind.Float32 => OscArg.Float32(AsFloat32()),
        OscArgKind.String => OscArg.String(AsString()),
        _ => throw new InvalidOperationException($"Unhandled {nameof(OscArgKind)} '{Kind}'."),
    };
}

/// <summary>
/// Decodes one OSC message in place. The exact inverse of <see cref="OscMessage.WriteTo"/>.
/// </summary>
/// <remarks>
/// <para>
/// Ported from v2's <c>Features/Transport/OSC/OscReader.cs</c>, which had already absorbed two audit
/// fixes worth keeping: unknown-but-sizeable tags advance the cursor rather than silently desynchronizing
/// every following argument (F-111), and nothing throws on a short buffer.
/// </para>
/// <para>
/// A malformed message sets <see cref="Malformed"/> and stops the enumeration. It never throws, because
/// the caller is a receive loop that must survive an arbitrary datagram from an arbitrary sender — see
/// <see cref="UdpOscReceiver"/> for why that is not negotiable.
/// </para>
/// </remarks>
public ref struct OscReader
{
    private static readonly SearchValues<byte> NullTerminator = SearchValues.Create([(byte)0]);

    private readonly ReadOnlySpan<byte> _packet;
    private readonly int _dataStart;
    private int _tagIndex;
    private int _dataOffset;

    private OscReader(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> address, ReadOnlySpan<byte> typeTags, int dataStart)
    {
        _packet = packet;
        _dataStart = dataStart;
        _dataOffset = dataStart;
        _tagIndex = 0;
        Address = address;
        TypeTags = typeTags;
        Malformed = false;
    }

    /// <summary>The address pattern, as raw UTF-8. Never materialized as a string by this type.</summary>
    public ReadOnlySpan<byte> Address { get; }

    /// <summary>The type tags, without the leading comma. One char per argument.</summary>
    public ReadOnlySpan<byte> TypeTags { get; }

    /// <summary>How many arguments the type-tag string declares.</summary>
    public readonly int ArgumentCount => TypeTags.Length;

    /// <summary>Set when an argument ran off the end of the buffer, or carried a tag we cannot size.</summary>
    public bool Malformed { get; private set; }

    /// <summary>Parses the header of a single (non-bundle) OSC message packet.</summary>
    /// <returns>False when the packet is not a well-formed message; nothing is thrown either way.</returns>
    public static bool TryParse(ReadOnlySpan<byte> packet, out OscReader reader)
    {
        reader = default;

        if (packet.Length == 0)
        {
            return false;
        }

        var addressEnd = packet.IndexOfAny(NullTerminator);
        if (addressEnd <= 0 || packet[0] != (byte)'/')
        {
            return false;
        }

        var address = packet[..addressEnd];

        var typeStart = Align4(addressEnd + 1);
        if (typeStart >= packet.Length || packet[typeStart] != (byte)',')
        {
            return false;
        }

        var typeEnd = packet[typeStart..].IndexOfAny(NullTerminator);
        if (typeEnd <= 0)
        {
            return false;
        }

        var typeTags = packet.Slice(typeStart + 1, typeEnd - 1);
        var dataStart = Math.Min(Align4(typeStart + typeEnd + 1), packet.Length);

        reader = new OscReader(packet, address, typeTags, dataStart);
        return true;
    }

    /// <summary>Rewinds to the first argument, so a sink may read the same message twice.</summary>
    public void Reset()
    {
        _tagIndex = 0;
        _dataOffset = _dataStart;
        Malformed = false;
    }

    /// <summary>Reads the next argument. False at the end of the list, or once <see cref="Malformed"/> is set.</summary>
    public bool TryReadNext(out OscValue value)
    {
        value = default;

        if (Malformed || _tagIndex >= TypeTags.Length)
        {
            return false;
        }

        var tag = (char)TypeTags[_tagIndex++];
        switch (tag)
        {
            // Per OSC 1.0 the tag IS the value: T and F carry zero argument bytes. Reading four here is
            // the classic decoder bug, and its symptom is every later argument in the message shifted.
            case 'T':
                value = OscValue.Bool(true);
                return true;

            case 'F':
                value = OscValue.Bool(false);
                return true;

            case 'i':
                if (!TryTake(4, out var intBytes))
                {
                    return Fail();
                }

                value = OscValue.Int32(BinaryPrimitives.ReadInt32BigEndian(intBytes));
                return true;

            case 'f':
                if (!TryTake(4, out var floatBytes))
                {
                    return Fail();
                }

                value = OscValue.Float32(BinaryPrimitives.ReadSingleBigEndian(floatBytes));
                return true;

            case 's':
            case 'S':
                if (!TryTakeString(out var text))
                {
                    return Fail();
                }

                value = tag == 's' ? OscValue.String(text) : OscValue.Unsupported(tag);
                return true;

            case 'b':
                if (!TryTakeBlob())
                {
                    return Fail();
                }

                value = OscValue.Unsupported(tag);
                return true;

            // Sizeable but unrepresentable. Skipping the right number of bytes is the whole point: v2's
            // F-111 was these falling through without advancing, which corrupted every later argument.
            case 'h':
            case 'd':
            case 't':
                if (!TryTake(8, out _))
                {
                    return Fail();
                }

                value = OscValue.Unsupported(tag);
                return true;

            case 'c':
            case 'r':
            case 'm':
                if (!TryTake(4, out _))
                {
                    return Fail();
                }

                value = OscValue.Unsupported(tag);
                return true;

            // Zero-byte tags. Nil, Infinitum, and the array delimiters.
            case 'N':
            case 'I':
            case '[':
            case ']':
                value = OscValue.Unsupported(tag);
                return true;

            default:
                // A tag whose width we do not know. Every argument after it is unreadable, so the
                // message is malformed rather than partially valid.
                return Fail();
        }
    }

    private bool Fail()
    {
        Malformed = true;
        return false;
    }

    private bool TryTake(int count, out ReadOnlySpan<byte> bytes)
    {
        if (_dataOffset + count > _packet.Length)
        {
            bytes = default;
            return false;
        }

        bytes = _packet.Slice(_dataOffset, count);
        _dataOffset += count;
        return true;
    }

    private bool TryTakeString(out ReadOnlySpan<byte> text)
    {
        text = default;

        if (_dataOffset >= _packet.Length)
        {
            return false;
        }

        var remaining = _packet[_dataOffset..];
        var end = remaining.IndexOfAny(NullTerminator);
        if (end < 0)
        {
            // Unterminated: OSC strings are always null-terminated and 4-byte padded, so this is a truncation.
            return false;
        }

        text = remaining[..end];
        _dataOffset = Align4(_dataOffset + end + 1);
        return true;
    }

    private bool TryTakeBlob()
    {
        if (!TryTake(4, out var sizeBytes))
        {
            return false;
        }

        var size = BinaryPrimitives.ReadInt32BigEndian(sizeBytes);
        if (size < 0 || _dataOffset + size > _packet.Length)
        {
            return false;
        }

        _dataOffset = Align4(_dataOffset + size);
        return true;
    }

    private static int Align4(int value) => (value + 3) & ~3;
}
