using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsCandidate(
    string TrackName,
    string ArtistName,
    string AlbumName,
    double DurationSeconds,
    bool Instrumental,
    bool HasSyncedLyrics);

public readonly record struct LyricsMatch(int Index, double Score);

public static class LyricsMatchScorer
{
    public const double BalancedThreshold = 0.62;

    /// <summary>Kept for callers that do not care about strictness.</summary>
    public const double AcceptThreshold = BalancedThreshold;

    private const double TitleWeight = 0.45;
    private const double ArtistWeight = 0.32;
    private const double DurationWeight = 0.23;

    private const double MinimumTitleSimilarity = 0.5;
    private const double MinimumArtistSimilarity = 0.34;

    // A record naming no version is barely penalised, since databases often file a version under the
    // plain song name. A record naming a different version is penalised hard: two versions of one
    // song share their title, artist and often their length.
    private const double QualifierAbsentFactor = 0.92;
    private const double QualifierUnwantedFactor = 0.85;
    private const double QualifierMismatchFactor = 0.55;
    private const double QualifierAgreementSimilarity = 0.6;

    public const double ExactDurationSeconds = 2;
    public const double CloseDurationSeconds = 5;
    public const double LooseDurationSeconds = 12;

    public static LyricsMatch PickBest(
        IReadOnlyList<LyricsCandidate> candidates,
        LyricsQuery query,
        LyricsMatchOptions options = default)
    {
        var best = new LyricsMatch(-1, 0);
        if (candidates == null)
            return best;

        double threshold = options.AcceptThreshold > 0 ? options.AcceptThreshold : BalancedThreshold;

        for (int i = 0; i < candidates.Count; i++)
        {
            double score = Score(candidates[i], query, options);
            if (score >= threshold && score > best.Score)
                best = new LyricsMatch(i, score);
        }

        return best;
    }

    public static double Score(LyricsCandidate candidate, LyricsQuery query, LyricsMatchOptions options = default)
    {
        if (!candidate.HasSyncedLyrics || candidate.Instrumental)
            return 0;

        double title = TitleScore(candidate.TrackName, query.Title);
        if (title < MinimumTitleSimilarity)
            return 0;

        double artist = Similarity(candidate.ArtistName, query.Artist);
        if (artist < MinimumArtistSimilarity)
            return 0;

        double duration = DurationScore(candidate.DurationSeconds, query.Duration, options.RequireCloseDuration);
        if (duration <= 0)
            return 0;

        return (title * TitleWeight) + (artist * ArtistWeight) + (duration * DurationWeight);
    }

    public static double DurationScore(double candidateSeconds, TimeSpan queried, bool requireClose = false)
    {
        // A guess is only tolerable while the title is still doing its share of the work.
        if (queried <= TimeSpan.Zero)
            return requireClose ? 0 : 0.55;

        if (candidateSeconds <= 0)
            return requireClose ? 0 : 0.35;

        double delta = Math.Abs(candidateSeconds - queried.TotalSeconds);

        if (delta <= ExactDurationSeconds)
            return 1.0;
        if (delta <= CloseDurationSeconds)
            return 0.75;
        if (requireClose)
            return 0;
        if (delta <= LooseDurationSeconds)
            return 0.4;

        return 0;
    }

    /// <summary>
    /// Judges the song and the version separately, so one version cannot pass as another on a token
    /// count alone.
    /// </summary>
    public static double TitleScore(string? candidateTitle, string? queryTitle)
    {
        var (candidateBase, candidateQualifier) = TitleQualifier.Split(candidateTitle);
        var (queryBase, queryQualifier) = TitleQualifier.Split(queryTitle);

        double baseSimilarity = Similarity(candidateBase, queryBase);

        // Splitting made things worse - the qualifier was the song. Judge the whole strings instead.
        if (baseSimilarity <= 0)
            return Similarity(candidateTitle, queryTitle);

        bool candidateHas = candidateQualifier.Length > 0;
        bool queryHas = queryQualifier.Length > 0;

        double factor = (candidateHas, queryHas) switch
        {
            (false, false) => 1.0,
            (false, true) => QualifierAbsentFactor,
            (true, false) => QualifierUnwantedFactor,
            (true, true) => Similarity(candidateQualifier, queryQualifier) >= QualifierAgreementSimilarity
                ? 1.0
                : QualifierMismatchFactor,
        };

        return baseSimilarity * factor;
    }

    public static double Similarity(string? left, string? right)
    {
        string a = Normalize(left);
        string b = Normalize(right);

        if (a.Length == 0 || b.Length == 0)
            return 0;

        if (string.Equals(a, b, StringComparison.Ordinal))
            return 1;

        var aTokens = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var bTokens = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (aTokens.Count == 0 || bTokens.Count == 0)
            return 0;

        int shared = aTokens.Count(bTokens.Contains);
        if (shared == 0)
            return 0;

        int smaller = Math.Min(aTokens.Count, bTokens.Count);
        int union = aTokens.Count + bTokens.Count - shared;

        double containment = (double)shared / smaller;
        double jaccard = (double)shared / union;

        return (containment * 0.65) + (jaccard * 0.35);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        bool lastWasSpace = true;

        foreach (char raw in value.Trim().ToLowerInvariant())
        {
            char c = Fold(raw);

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static char Fold(char c) => c switch
    {
        'á' or 'à' or 'â' or 'ä' or 'ã' or 'å' => 'a',
        'é' or 'è' or 'ê' or 'ë' => 'e',
        'í' or 'ì' or 'î' or 'ï' => 'i',
        'ó' or 'ò' or 'ô' or 'ö' or 'õ' or 'ø' => 'o',
        'ú' or 'ù' or 'û' or 'ü' => 'u',
        'ñ' => 'n',
        'ç' => 'c',
        _ => c,
    };
}
