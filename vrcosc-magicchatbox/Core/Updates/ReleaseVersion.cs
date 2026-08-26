using System;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Core.Updates;

public static partial class ReleaseVersion
{
    [GeneratedRegex(@"^\s*v?(\d+(?:\.\d+)*)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumber();

    public static string Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        Match match = LeadingNumber().Match(tag);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public static int Compare(string? left, string? right)
    {
        (string leftNumbers, string leftSuffix) = Split(left);
        (string rightNumbers, string rightSuffix) = Split(right);

        int byNumber = CompareNumbers(leftNumbers, rightNumbers);
        if (byNumber != 0)
            return byNumber;

        // Semantic versioning ranks a suffixed build below the plain release it leads up to,
        // so an -rc build must never be treated as newer than the version it is a candidate for.
        bool leftIsPre = leftSuffix.Length > 0;
        bool rightIsPre = rightSuffix.Length > 0;

        if (leftIsPre != rightIsPre)
            return leftIsPre ? -1 : 1;

        return string.CompareOrdinal(leftSuffix, rightSuffix);
    }

    private static (string Numbers, string Suffix) Split(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return (string.Empty, string.Empty);

        Match match = LeadingNumber().Match(tag);
        if (!match.Success)
            return (string.Empty, string.Empty);

        string remainder = tag[(match.Index + match.Length)..];

        int buildMetadata = remainder.IndexOf('+');
        if (buildMetadata >= 0)
            remainder = remainder[..buildMetadata];

        return (match.Groups[1].Value, remainder.Trim().TrimStart('-', '.'));
    }

    private static int CompareNumbers(string left, string right)
    {
        string[] leftParts = left.Length == 0 ? [] : left.Split('.');
        string[] rightParts = right.Length == 0 ? [] : right.Split('.');
        int length = Math.Max(leftParts.Length, rightParts.Length);

        for (int i = 0; i < length; i++)
        {
            int leftSegment = SegmentAt(leftParts, i);
            int rightSegment = SegmentAt(rightParts, i);

            if (leftSegment != rightSegment)
                return leftSegment.CompareTo(rightSegment);
        }

        return 0;
    }

    private static int SegmentAt(string[] parts, int index)
        => index < parts.Length && int.TryParse(parts[index], out int value) ? value : 0;
}
