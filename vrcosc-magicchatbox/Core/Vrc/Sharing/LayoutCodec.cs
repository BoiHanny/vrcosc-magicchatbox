using MagicChatbox.Vocabulary;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace vrcosc_magicchatbox.Core.Vrc.Sharing;

public enum LayoutRejection
{
    None,
    Empty,
    TooLarge,
    NotJson,
    WrongKind,
    UnsupportedSchema,
    TooManyRequirements,
    IllegalName,
    BadCode,
}

public sealed record LayoutParseResult(LayoutDocument? Document, LayoutRejection Rejection, string Detail)
{
    public bool Ok => Document != null && Rejection == LayoutRejection.None;
}

public enum LayoutMatch
{
    Present,
    Missing,
    WrongType,
    NotWritable,
}

public sealed record LayoutMatchRow(string Name, string Type, bool Optional, string Purpose, LayoutMatch Match);

public sealed record LayoutMatchReport(IReadOnlyList<LayoutMatchRow> Rows, int Present, int MissingRequired)
{
    public bool Satisfied => MissingRequired == 0;
}

public static class LayoutCodec
{
    public const int MaxBytes = 256 * 1024;
    public const int MaxRequirements = 512;
    public const int MaxTags = 16;
    public const int MaxTextLength = 400;
    public const string CodePrefix = "MCBL1-";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Write(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Kind = LayoutDocument.ExpectedKind;
        document.Schema = LayoutDocument.CurrentSchema;

        return JsonSerializer.Serialize(document, Options);
    }

    public static LayoutParseResult Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LayoutParseResult(null, LayoutRejection.Empty, "There is nothing to read.");

        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
            return new LayoutParseResult(null, LayoutRejection.TooLarge, "That layout is too big to be genuine.");

        LayoutDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<LayoutDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            return new LayoutParseResult(null, LayoutRejection.NotJson, ex.Message);
        }

        if (document == null)
            return new LayoutParseResult(null, LayoutRejection.NotJson, "That is not a layout.");

        if (!string.Equals(document.Kind, LayoutDocument.ExpectedKind, StringComparison.Ordinal))
            return new LayoutParseResult(null, LayoutRejection.WrongKind, "That file is not a MagicChatbox layout.");

        if (document.Schema is < 1 or > LayoutDocument.CurrentSchema)
        {
            return new LayoutParseResult(
                null, LayoutRejection.UnsupportedSchema,
                "That layout was made by a newer version of MagicChatbox.");
        }

        document.Requires ??= new List<LayoutRequirement>();
        document.Tags ??= new List<string>();

        if (document.Requires.Count > MaxRequirements)
        {
            return new LayoutParseResult(
                null, LayoutRejection.TooManyRequirements,
                $"A layout may ask for at most {MaxRequirements} parameters.");
        }

        foreach (LayoutRequirement requirement in document.Requires)
        {
            if (!AvatarParameterAddress.TryResolveUntrusted(requirement.Name, out _))
            {
                return new LayoutParseResult(
                    null, LayoutRejection.IllegalName,
                    $"A parameter name in that layout is not one VRChat can address: {Describe(requirement.Name)}");
            }
        }

        document.Title = Clip(document.Title);
        document.Description = Clip(document.Description);
        document.Author = Clip(document.Author);
        document.License = Clip(document.License);
        document.Tags = document.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Take(MaxTags).Select(Clip).ToList();

        return new LayoutParseResult(document, LayoutRejection.None, string.Empty);
    }

    public static string ToCode(LayoutDocument document)
    {
        byte[] raw = Encoding.UTF8.GetBytes(Write(document));

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw, 0, raw.Length);

        return CodePrefix + Convert.ToBase64String(output.ToArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static LayoutParseResult FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new LayoutParseResult(null, LayoutRejection.Empty, "There is nothing to read.");

        string trimmed = code.Trim();

        if (!trimmed.StartsWith(CodePrefix, StringComparison.Ordinal))
            return new LayoutParseResult(null, LayoutRejection.BadCode, "That does not look like a layout code.");

        string payload = trimmed[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        byte[] compressed;

        try
        {
            compressed = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return new LayoutParseResult(null, LayoutRejection.BadCode, "That layout code is damaged.");
        }

        try
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            var buffer = new byte[8192];
            int total = 0;
            int read;

            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;

                if (total > MaxBytes)
                    return new LayoutParseResult(null, LayoutRejection.TooLarge, "That layout is too big to be genuine.");

                output.Write(buffer, 0, read);
            }

            return Read(Encoding.UTF8.GetString(output.ToArray()));
        }
        catch (InvalidDataException)
        {
            return new LayoutParseResult(null, LayoutRejection.BadCode, "That layout code is damaged.");
        }
    }

    public static LayoutMatchReport Match(LayoutDocument document, AvatarSchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(schema);

        AvatarSchemaLookup declared = AvatarSchemaIndex.ByNormalizedName(schema.Parameters);

        var rows = new List<LayoutMatchRow>();
        int present = 0;
        int missingRequired = 0;

        foreach (LayoutRequirement requirement in document.Requires)
        {
            string key = EcosystemSignature.Normalize(requirement.Name);
            LayoutMatch match;

            if (!declared.TryGet(key, out var declaration))
            {
                match = LayoutMatch.Missing;
            }
            else if (!string.Equals(declaration.Kind.ToString(), requirement.Type, StringComparison.OrdinalIgnoreCase))
            {
                match = LayoutMatch.WrongType;
            }
            else if (!declaration.Writable)
            {
                match = LayoutMatch.NotWritable;
            }
            else
            {
                match = LayoutMatch.Present;
                present++;
            }

            if (match != LayoutMatch.Present && !requirement.Optional)
                missingRequired++;

            rows.Add(new LayoutMatchRow(
                requirement.Name, requirement.Type, requirement.Optional, requirement.Purpose, match));
        }

        return new LayoutMatchReport(rows, present, missingRequired);
    }

    private static string Clip(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string cleaned = new(text.Where(c => !char.IsControl(c)).ToArray());

        return cleaned.Length <= MaxTextLength ? cleaned : cleaned[..MaxTextLength];
    }

    private static string Describe(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "(blank)";

        string safe = new(name.Where(c => !char.IsControl(c)).Take(60).ToArray());
        return safe.Length == 0 ? "(blank)" : safe;
    }
}
