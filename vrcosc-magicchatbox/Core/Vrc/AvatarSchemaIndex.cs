using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record AvatarSchemaLookup(
    IReadOnlyDictionary<string, VrcParameterDeclaration> ByName,
    int Ambiguous)
{
    public bool TryGet(string name, out VrcParameterDeclaration declaration)
        => ByName.TryGetValue(name, out declaration);

    public bool Contains(string name) => ByName.ContainsKey(name);
}

public static class AvatarSchemaIndex
{
    public static AvatarSchemaLookup ByNormalizedName(IEnumerable<VrcParameterDeclaration> parameters)
        => Build(parameters, EcosystemSignature.Normalize, StringComparer.Ordinal);

    public static AvatarSchemaLookup ByExactName(
        IEnumerable<VrcParameterDeclaration> parameters,
        StringComparer? comparer = null)
        => Build(parameters, name => name, comparer ?? StringComparer.Ordinal);

    private static AvatarSchemaLookup Build(
        IEnumerable<VrcParameterDeclaration> parameters,
        Func<string, string> key,
        StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var map = new Dictionary<string, VrcParameterDeclaration>(comparer);
        int ambiguous = 0;

        foreach (VrcParameterDeclaration declaration in parameters)
        {
            string name = declaration.Name ?? string.Empty;

            if (name.Length == 0)
                continue;

            string mapped = key(name);

            if (mapped.Length == 0)
                continue;

            if (!map.TryGetValue(mapped, out VrcParameterDeclaration existing))
            {
                map[mapped] = declaration;
                continue;
            }

            ambiguous++;

            bool existingIsExact = comparer.Equals(existing.Name, mapped);
            bool candidateIsExact = comparer.Equals(name, mapped);

            if (candidateIsExact && !existingIsExact)
                map[mapped] = declaration;
        }

        return new AvatarSchemaLookup(map, ambiguous);
    }
}
