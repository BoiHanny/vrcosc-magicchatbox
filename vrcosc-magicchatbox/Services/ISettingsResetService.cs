using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Services;

public interface ISettingsResetService
{
    int ResetAll<T>(ISettingsProvider<T> provider, bool preserveCredentials = true) where T : class, new();
    int ResetProperties<T>(ISettingsProvider<T> provider, IEnumerable<string> propertyNames, bool preserveCredentials = true) where T : class, new();
}
