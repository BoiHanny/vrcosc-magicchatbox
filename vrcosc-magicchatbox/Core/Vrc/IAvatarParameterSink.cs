using System;

namespace vrcosc_magicchatbox.Core.Vrc;

public interface IAvatarParameterSink
{
    void Set(string name, bool value);

    void Set(string name, int value);

    void Set(string name, float value);

    void Pulse(string name, int milliseconds = 150);
}

public static class AvatarParameterAddress
{
    public const int MaxNameLength = 200;

    private static readonly char[] IllegalInName = [' ', '#', '*', ',', '?', '[', ']', '{', '}'];

    public static string Resolve(string nameOrAddress) => ResolveTrusted(nameOrAddress);

    public static string ResolveTrusted(string nameOrAddress)
    {
        if (string.IsNullOrWhiteSpace(nameOrAddress))
            return string.Empty;

        string trimmed = nameOrAddress.Trim();

        return trimmed.StartsWith('/')
            ? trimmed
            : AvatarParameter.AddressPrefix + trimmed;
    }

    public static bool TryResolveUntrusted(string? name, out string address)
    {
        address = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        string trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
            return false;

        if (trimmed.StartsWith('/'))
            return false;

        if (trimmed.Contains("/avatar/", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (char c in trimmed)
        {
            if (char.IsControl(c) || c == '\0')
                return false;
        }

        if (trimmed.IndexOfAny(IllegalInName) >= 0)
            return false;

        address = AvatarParameter.AddressPrefix + trimmed;
        return true;
    }

    public static string ToName(string nameOrAddress)
    {
        if (string.IsNullOrWhiteSpace(nameOrAddress))
            return string.Empty;

        string trimmed = nameOrAddress.Trim();

        return trimmed.StartsWith(AvatarParameter.AddressPrefix, StringComparison.Ordinal)
            ? trimmed[AvatarParameter.AddressPrefix.Length..]
            : trimmed.TrimStart('/');
    }
}
