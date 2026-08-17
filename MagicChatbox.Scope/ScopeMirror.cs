using System.Collections.Immutable;
using System.Text;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Scope;

/// <summary>Turns a fact key and a value into words a person recognises.</summary>
public interface IScopeFactNames
{
    /// <summary>What to call this fact. Return null to fall back to the raw key.</summary>
    string? NameFor(ScopeFactKey key);

    /// <summary>What to call this value under this fact. Return null to render it plainly.</summary>
    string? ValueFor(ScopeFactKey key, SignalValue value);
}

/// <summary>
/// Renders a guard as one English sentence.
/// </summary>
/// <remarks>
/// <b>A renderer, never a parser.</b> No text expression language ships: the only way to build a guard is
/// the editor, so there is nothing to round-trip and no grammar anybody has to keep two implementations
/// of in step. The previous generation shipped a tokenizer, a parser and a serializer for its condition
/// text, and no user ever typed one.
/// <para>
/// Two forms. <see cref="Canonical"/> uses raw keys and is the only form stored, logged or diffed, so a
/// display-name change cannot look like an edit. <see cref="Friendly"/> is for reading.
/// </para>
/// </remarks>
public static class ScopeMirror
{
    public static string Canonical(ScopeGroup group) => Render(group, names: null, top: true);

    public static string Friendly(ScopeGroup group, IScopeFactNames? names) => Render(group, names, top: true);

    private static string Render(ScopeGroup group, IScopeFactNames? names, bool top)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.IsEmpty)
            return group.Join == ScopeJoin.Any ? "never" : "always";

        var parts = new List<string>();

        foreach (ScopePredicate predicate in group.SafePredicates)
            parts.Add(RenderPredicate(predicate, names));

        foreach (ScopeGroup nested in group.SafeGroups)
            parts.Add(Render(nested, names, top: false));

        string joiner = group.Join switch
        {
            ScopeJoin.All => " and ",
            ScopeJoin.Any => " or ",
            ScopeJoin.None => " or ",
        };

        string body = string.Join(joiner, parts);

        if (group.Join == ScopeJoin.None)
            body = parts.Count == 1 ? $"not {body}" : $"none of ({body})";
        else if (!top && parts.Count > 1)
            body = $"({body})";

        return body;
    }

    private static string RenderPredicate(ScopePredicate predicate, IScopeFactNames? names)
    {
        string fact = names?.NameFor(predicate.Key) ?? predicate.Key.Value;

        switch (predicate.Op)
        {
            case ScopeOperator.IsLive:
                return $"{fact} is known";
            case ScopeOperator.IsNotLive:
                return $"{fact} is not known";
        }

        string value = names?.ValueFor(predicate.Key, predicate.Value) ?? Plain(predicate.Value);

        return predicate.Op switch
        {
            ScopeOperator.Equals => $"{fact} is {value}",
            ScopeOperator.NotEquals => $"{fact} is not {value}",
            ScopeOperator.Contains => $"{fact} contains {value}",
            ScopeOperator.GreaterThan => $"{fact} is above {value}",
            ScopeOperator.GreaterOrEqual => $"{fact} is {value} or more",
            ScopeOperator.LessThan => $"{fact} is below {value}",
            ScopeOperator.LessOrEqual => $"{fact} is {value} or less",
            ScopeOperator.InGroup => $"{fact} is {value}",
            ScopeOperator.IsLive => $"{fact} is known",
            ScopeOperator.IsNotLive => $"{fact} is not known",
        };
    }

    /// <summary>Why a guard is not saying yes, in one clause, or empty when it is.</summary>
    public static string Because(ScopeOutcome outcome, ScopeBlock block, IScopeFactNames? names)
    {
        if (outcome == ScopeOutcome.True || !block.HasKey)
            return string.Empty;

        string fact = names?.NameFor(block.Key) ?? block.Key.Value;

        return block.WasUnknown
            ? $"waiting on {fact}"
            : $"{fact} does not match";
    }

    private static string Plain(SignalValue value) => value.Kind switch
    {
        SignalKind.Bool => value.AsBool() ? "on" : "off",
        SignalKind.Int => value.AsInt().ToString(System.Globalization.CultureInfo.InvariantCulture),
        SignalKind.Float => value.AsFloat().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        SignalKind.Text => value.AsText(),
    };
}
