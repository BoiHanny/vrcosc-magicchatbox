using System.Buffers.Binary;
using System.Text;

namespace MagicChatbox.Osc;

/// <summary>The four OSC argument types VRChat actually uses.</summary>
/// <remarks>
/// VRChat sends exactly <c>T</c>, <c>F</c>, <c>i</c> and <c>f</c> for avatar parameters, and <c>s</c> on
/// <c>/avatar/change</c>. VRCNext's parser switches on precisely those and returns on anything else
/// (<c>VRCNext/Services/OscService.cs:139-159</c>). Supporting more would be speculative surface on the
/// hottest path in the application.
/// <para>
/// Per OSC 1.0, <c>T</c> and <c>F</c> carry <b>no argument bytes at all</b> — the type tag *is* the
/// value. Forgetting that is the classic OSC encoder bug, so it is asserted in the tests.
/// </para>
/// </remarks>
public enum OscArgKind : byte
{
    Bool,
    Int32,
    Float32,
    String,
}

/// <summary>One OSC argument.</summary>
public readonly struct OscArg
{
    private readonly double _numeric;
    private readonly string? _text;

    private OscArg(OscArgKind kind, double numeric, string? text)
    {
        Kind = kind;
        _numeric = numeric;
        _text = text;
    }

    public OscArgKind Kind { get; }

    public static OscArg Bool(bool v) => new(OscArgKind.Bool, v ? 1 : 0, null);

    public static OscArg Int32(int v) => new(OscArgKind.Int32, v, null);

    public static OscArg Float32(float v) => new(OscArgKind.Float32, v, null);

    public static OscArg String(string v) =>
        new(OscArgKind.String, 0, v ?? throw new ArgumentNullException(nameof(v)));

    public bool AsBool() => _numeric != 0;

    public int AsInt32() => (int)_numeric;

    public float AsFloat32() => (float)_numeric;

    public string AsString() => _text ?? string.Empty;

    /// <summary>The OSC type-tag character for this argument.</summary>
    public char TypeTag => Kind switch
    {
        OscArgKind.Bool => AsBool() ? 'T' : 'F',
        OscArgKind.Int32 => 'i',
        OscArgKind.Float32 => 'f',
        OscArgKind.String => 's',
        _ => throw new InvalidOperationException($"Unhandled {nameof(OscArgKind)} '{Kind}'."),
    };
}

/// <summary>An OSC message: an address pattern and its arguments.</summary>
public sealed class OscMessage
{
    private readonly OscArg[] _args;

    private OscMessage(string address, OscArg[] args)
    {
        Address = address;
        _args = args;
    }

    public string Address { get; }

    public ReadOnlySpan<OscArg> Args => _args;

    public static OscMessage Create(string address, params OscArg[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (address[0] != '/')
        {
            throw new ArgumentException($"OSC address must start with '/'. Got '{address}'.", nameof(address));
        }

        return new OscMessage(address, args ?? []);
    }

    public static OscMessage Create(string address, string text, bool sendImmediately, bool playSfx) =>
        Create(address, OscArg.String(text), OscArg.Bool(sendImmediately), OscArg.Bool(playSfx));

    /// <summary>
    /// Encodes this message into <paramref name="destination"/>, returning the bytes written.
    /// </summary>
    /// <remarks>
    /// Big-endian, with every block padded to a 4-byte boundary — both mandated by OSC 1.0 and both
    /// places a hand-rolled encoder goes wrong. <c>T</c>/<c>F</c> contribute a type tag and zero
    /// argument bytes.
    /// </remarks>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public int WriteTo(Span<byte> destination)
    {
        var written = 0;
        written += WritePaddedString(destination, Address);

        // Type tag string: ',' followed by one char per argument, then padded.
        Span<char> tags = _args.Length <= 64 ? stackalloc char[_args.Length + 1] : new char[_args.Length + 1];
        tags[0] = ',';
        for (var i = 0; i < _args.Length; i++)
        {
            tags[i + 1] = _args[i].TypeTag;
        }

        written += WritePaddedString(destination[written..], new string(tags));

        foreach (var arg in _args)
        {
            switch (arg.Kind)
            {
                case OscArgKind.Bool:
                    break; // No argument bytes. The tag carried it.

                case OscArgKind.Int32:
                    EnsureRoom(destination, written, 4);
                    BinaryPrimitives.WriteInt32BigEndian(destination[written..], arg.AsInt32());
                    written += 4;
                    break;

                case OscArgKind.Float32:
                    EnsureRoom(destination, written, 4);
                    BinaryPrimitives.WriteSingleBigEndian(destination[written..], arg.AsFloat32());
                    written += 4;
                    break;

                case OscArgKind.String:
                    written += WritePaddedString(destination[written..], arg.AsString());
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled {nameof(OscArgKind)} '{arg.Kind}'.");
            }
        }

        return written;
    }

    /// <summary>Upper bound on the encoded size, for buffer rental.</summary>
    public int MaxEncodedSize()
    {
        var size = Padded(Encoding.UTF8.GetByteCount(Address) + 1)
                 + Padded(_args.Length + 2);

        foreach (var arg in _args)
        {
            size += arg.Kind switch
            {
                OscArgKind.Bool => 0,
                OscArgKind.String => Padded(Encoding.UTF8.GetByteCount(arg.AsString()) + 1),
                _ => 4,
            };
        }

        return size;
    }

    private static int Padded(int length) => (length + 3) & ~3;

    private static void EnsureRoom(Span<byte> destination, int offset, int needed)
    {
        if (destination.Length - offset < needed)
        {
            throw new ArgumentException("Destination buffer is too small for this OSC message.", nameof(destination));
        }
    }

    private static int WritePaddedString(Span<byte> destination, string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var total = Padded(byteCount + 1); // At least one null terminator, then pad to 4.

        if (destination.Length < total)
        {
            throw new ArgumentException("Destination buffer is too small for this OSC message.", nameof(destination));
        }

        Encoding.UTF8.GetBytes(value, destination);
        destination[byteCount..total].Clear();
        return total;
    }
}
