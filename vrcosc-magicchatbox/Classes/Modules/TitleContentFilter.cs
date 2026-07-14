using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes.Modules;

internal static class TitleContentFilter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
    private static readonly Regex RepeatedHorizontalWhitespace = new("[ \t]{2,}", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly object InvalidRuleLock = new();
    private static readonly HashSet<string> InvalidRulesLogged = new(StringComparer.Ordinal);

    public static string ApplyRegexTransform(string text, string expression)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(expression))
            return text;

        string pattern = expression.Trim();
        string? replacement = null;
        if (TrySplitReplacement(pattern, out string splitPattern, out string splitReplacement))
        {
            pattern = splitPattern;
            replacement = splitReplacement;
        }

        try
        {
            Regex regex = new(pattern, RegexOptions.CultureInvariant, RegexTimeout);

            if (replacement != null)
            {
                string replaced = regex.Replace(text, match => match.Result(replacement));
                return Normalize(replaced);
            }

            Match match = regex.Match(text);
            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                return Normalize(match.Groups[1].Value);
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            LogInvalidRule(expression, ex);
        }

        return text;
    }

    public static bool MatchesAny(string text, string patterns)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (ContentRule rule in ParseRules(patterns))
        {
            if (rule.IsMatch(text))
                return true;
        }

        return false;
    }

    public static string RemoveMatches(string text, string patterns)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string output = text;
        bool modified = false;

        foreach (ContentRule rule in ParseRules(patterns))
        {
            string next = rule.RemoveFrom(output);
            if (!string.Equals(next, output, StringComparison.Ordinal))
            {
                output = next;
                modified = true;
            }
        }

        return modified ? Normalize(output) : text;
    }

    private static IEnumerable<ContentRule> ParseRules(string patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
            yield break;

        foreach (string rawPart in patterns.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ContentRule? rule = CreateRule(rawPart);
            if (rule != null)
                yield return rule;
        }
    }

    private static ContentRule? CreateRule(string rawRule)
    {
        string rule = rawRule.Trim();
        if (rule.Length == 0)
            return null;

        string pattern;
        string? replacement = null;
        RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

        if (rule.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            string body = rule["regex:".Length..].Trim();
            if (TrySplitReplacement(body, out string splitPattern, out string splitReplacement))
            {
                body = splitPattern;
                replacement = splitReplacement;
            }

            pattern = body;
        }
        else if (TryParseSlashRegex(rule, out pattern, out replacement, out bool ignoreCase))
        {
            if (!ignoreCase)
                options &= ~RegexOptions.IgnoreCase;
        }
        else
        {
            pattern = BuildLiteralPattern(rule);
        }

        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        try
        {
            return new ContentRule(rule, new Regex(pattern, options, RegexTimeout), replacement);
        }
        catch (ArgumentException ex)
        {
            LogInvalidRule(rule, ex);
            return null;
        }
    }

    private static bool TryParseSlashRegex(string rule, out string pattern, out string? replacement, out bool ignoreCase)
    {
        pattern = string.Empty;
        replacement = null;
        ignoreCase = false;

        if (!rule.StartsWith('/'))
            return false;

        int closingSlash = FindClosingSlash(rule);
        if (closingSlash <= 0)
            return false;

        pattern = rule[1..closingSlash];
        string suffix = rule[(closingSlash + 1)..].TrimStart();
        int flagLength = 0;
        while (flagLength < suffix.Length && char.IsLetter(suffix[flagLength]))
        {
            if (suffix[flagLength] == 'i')
                ignoreCase = true;
            flagLength++;
        }

        suffix = suffix[flagLength..].TrimStart();
        if (suffix.StartsWith("=>", StringComparison.Ordinal))
            replacement = DecodeReplacement(suffix[2..].Trim());

        return true;
    }

    private static int FindClosingSlash(string value)
    {
        bool escaped = false;
        for (int i = 1; i < value.Length; i++)
        {
            char ch = value[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '/')
                return i;
        }

        return -1;
    }

    private static bool TrySplitReplacement(string value, out string pattern, out string replacement)
    {
        int separatorIndex = value.LastIndexOf("=>", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            pattern = value.Trim();
            replacement = string.Empty;
            return false;
        }

        pattern = value[..separatorIndex].Trim();
        replacement = DecodeReplacement(value[(separatorIndex + 2)..].Trim());
        return true;
    }

    private static string DecodeReplacement(string replacement)
    {
        return replacement
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
    }

    private static string BuildLiteralPattern(string value)
    {
        return Regex.Escape(value);
    }

    private static string Normalize(string value)
    {
        try
        {
            return RepeatedHorizontalWhitespace.Replace(value, " ").Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return value.Trim();
        }
    }

    private static void LogInvalidRule(string rule, Exception ex)
    {
        lock (InvalidRuleLock)
        {
            if (!InvalidRulesLogged.Add(rule))
                return;
        }

        Logging.WriteException(new Exception($"Invalid title content filter rule '{rule}': {ex.Message}", ex), MSGBox: false);
    }

    private sealed class ContentRule
    {
        private readonly Regex _regex;
        private readonly string _source;
        private readonly string? _replacement;

        public ContentRule(string source, Regex regex, string? replacement)
        {
            _source = source;
            _regex = regex;
            _replacement = replacement;
        }

        public bool IsMatch(string text)
        {
            try
            {
                return _regex.IsMatch(text);
            }
            catch (RegexMatchTimeoutException ex)
            {
                LogInvalidRule(_source, ex);
                return false;
            }
        }

        public string RemoveFrom(string text)
        {
            try
            {
                return _regex.Replace(text, match =>
                {
                    if (_replacement != null)
                        return match.Result(_replacement);

                    return ShouldBridgeWithSpace(text, match) ? " " : string.Empty;
                });
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                LogInvalidRule(_source, ex);
                return text;
            }
        }

        private static bool ShouldBridgeWithSpace(string text, Match match)
        {
            int beforeIndex = match.Index - 1;
            int afterIndex = match.Index + match.Length;
            return beforeIndex >= 0
                && afterIndex < text.Length
                && !char.IsWhiteSpace(text[beforeIndex])
                && !char.IsWhiteSpace(text[afterIndex]);
        }
    }
}
