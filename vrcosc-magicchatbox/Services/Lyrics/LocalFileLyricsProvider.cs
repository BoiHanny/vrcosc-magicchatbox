using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Services.Lyrics;

public sealed class LocalFileLyricsProvider : ILyricsProvider
{
    public const string ProviderName = "Local file";

    private readonly Func<string> _folderResolver;

    public LocalFileLyricsProvider(Func<string> folderResolver)
    {
        _folderResolver = folderResolver;
    }

    public string Name => ProviderName;

    public bool RequiresInternet => false;

    public Task<LyricTrack?> TryGetAsync(LyricsQuery query, CancellationToken ct = default)
    {
        if (!query.IsUsable)
            return Task.FromResult<LyricTrack?>(null);

        string folder = _folderResolver();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Task.FromResult<LyricTrack?>(null);

        try
        {
            string? match = FindFile(folder, query);
            if (match == null)
                return Task.FromResult<LyricTrack?>(null);

            var track = LrcParser.Parse(File.ReadAllText(match), ProviderName);
            return Task.FromResult<LyricTrack?>(track.IsSynced ? track : null);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Local lyrics lookup failed: {ex.Message}");
            return Task.FromResult<LyricTrack?>(null);
        }
    }

    private static string? FindFile(string folder, LyricsQuery query)
    {
        string exact = Path.Combine(folder, Sanitize($"{query.Artist} - {query.Title}") + ".lrc");
        if (File.Exists(exact))
            return exact;

        string wanted = Normalize($"{query.Artist}{query.Title}");

        return Directory
            .EnumerateFiles(folder, "*.lrc", SearchOption.AllDirectories)
            .FirstOrDefault(path => Normalize(Path.GetFileNameWithoutExtension(path)) == wanted);
    }

    public static string Sanitize(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return name;
    }

    public static string Normalize(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
