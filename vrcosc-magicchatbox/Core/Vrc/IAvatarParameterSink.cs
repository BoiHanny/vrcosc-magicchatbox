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
    public static string Resolve(string nameOrAddress)
    {
        if (string.IsNullOrWhiteSpace(nameOrAddress))
            return string.Empty;

        string trimmed = nameOrAddress.Trim();

        return trimmed.StartsWith('/')
            ? trimmed
            : AvatarParameter.AddressPrefix + trimmed;
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
