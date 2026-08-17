using System;

namespace vrcosc_magicchatbox.Core.Configuration;

public interface ISettingsProvider<T> where T : class, new()
{
    T Value { get; }

    void Save();

    void FlushPendingSave();

    void Reload();

    bool LoadedFromFile => true;

    event EventHandler SettingsChanged;
}
