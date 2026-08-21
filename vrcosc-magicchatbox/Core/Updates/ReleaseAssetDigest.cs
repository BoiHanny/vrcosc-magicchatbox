using System;
using System.IO;
using System.Security.Cryptography;

namespace vrcosc_magicchatbox.Core.Updates;

public enum DigestVerificationStatus
{
    NotPublished,
    Match,
    Mismatch
}

public readonly record struct DigestVerificationResult(
    DigestVerificationStatus Status,
    string? Expected,
    string? Actual)
{
    public bool IsRejected => Status == DigestVerificationStatus.Mismatch;
}

public readonly record struct ReleaseAssetDigest(string Sha256Hex)
{
    private const string Sha256Prefix = "sha256:";
    private const int Sha256HexLength = 64;

    public static bool TryParse(string? value, out ReleaseAssetDigest digest)
    {
        digest = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        if (trimmed.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[Sha256Prefix.Length..];
        }

        if (trimmed.Length != Sha256HexLength)
        {
            return false;
        }

        foreach (char c in trimmed)
        {
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        digest = new ReleaseAssetDigest(trimmed.ToLowerInvariant());
        return true;
    }

    public bool Matches(string? sha256Hex) =>
        !string.IsNullOrWhiteSpace(sha256Hex) &&
        string.Equals(Sha256Hex, sha256Hex.Trim(), StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Sha256Prefix + Sha256Hex;

    public static string ComputeSha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    public static string ComputeSha256File(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ComputeSha256(stream);
    }

    public static DigestVerificationResult Verify(string? publishedDigest, string filePath)
    {
        if (!TryParse(publishedDigest, out ReleaseAssetDigest expected))
        {
            return new DigestVerificationResult(DigestVerificationStatus.NotPublished, null, null);
        }

        string actual = ComputeSha256File(filePath);

        return expected.Matches(actual)
            ? new DigestVerificationResult(DigestVerificationStatus.Match, expected.Sha256Hex, actual)
            : new DigestVerificationResult(DigestVerificationStatus.Mismatch, expected.Sha256Hex, actual);
    }
}
