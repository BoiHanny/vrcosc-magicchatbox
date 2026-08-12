using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Services.Lyrics;

public interface ILyricsProvider
{
    string Name { get; }

    bool RequiresInternet { get; }

    Task<LyricTrack?> TryGetAsync(LyricsQuery query, CancellationToken ct = default);
}
