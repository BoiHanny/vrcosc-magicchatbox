using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace vrcosc_magicchatbox.Services.Voicemod;

public sealed class VoicemodArtworkStoredEventArgs : EventArgs
{
    public VoicemodArtworkStoredEventArgs(string key) => Key = key;

    public string Key { get; }
}

public interface IVoicemodArtworkCache
{
    event EventHandler<VoicemodArtworkStoredEventArgs>? ArtworkStored;

    ImageSource? Get(string kind, string id);

    bool Contains(string kind, string id);

    bool Store(string kind, string id, string base64);

    void Clear();
}

public sealed class VoicemodArtworkCache : IVoicemodArtworkCache
{
    private const int MaximumEntries = 400;

    private const int MaximumBytes = 512 * 1024;

    /// <summary>Covers the largest seat the artwork gets (33px) at 200% DPI, with headroom.</summary>
    private const int DecodeWidth = 96;

    private readonly ConcurrentDictionary<string, ImageSource> _images = new(StringComparer.OrdinalIgnoreCase);

    // Insertion order, so a full cache can drop its oldest entry instead of refusing every new one.
    // A library of well over a thousand sounds browses past the cap long before you stop scrolling.
    private readonly ConcurrentQueue<string> _order = new();

    public event EventHandler<VoicemodArtworkStoredEventArgs>? ArtworkStored;

    public static string BuildKey(string kind, string id) => $"{kind}:{id}";

    public ImageSource? Get(string kind, string id)
        => _images.TryGetValue(BuildKey(kind, id), out ImageSource? image) ? image : null;

    public bool Contains(string kind, string id) => _images.ContainsKey(BuildKey(kind, id));

    public bool Store(string kind, string id, string base64)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(base64))
            return false;

        if (!TryDecode(base64, out ImageSource? image) || image == null)
            return false;

        string key = BuildKey(kind, id);
        if (_images.TryAdd(key, image))
            _order.Enqueue(key);
        else
            _images[key] = image;

        while (_images.Count > MaximumEntries && _order.TryDequeue(out string? oldest))
        {
            if (!string.Equals(oldest, key, StringComparison.OrdinalIgnoreCase))
                _images.TryRemove(oldest, out _);
        }

        ArtworkStored?.Invoke(this, new VoicemodArtworkStoredEventArgs(key));
        return true;
    }

    public void Clear()
    {
        _images.Clear();
        while (_order.TryDequeue(out _))
        {
        }
    }

    public static bool TryDecode(string base64, out ImageSource? image)
    {
        image = null;

        string payload = base64.Trim();
        int comma = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            payload = payload[(comma + 1)..];

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length == 0 || bytes.Length > MaximumBytes)
            return false;

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;

            // These are shown at ~33px. Without a decode size the full-resolution pixel buffer is what
            // stays resident, once per cached sound.
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
