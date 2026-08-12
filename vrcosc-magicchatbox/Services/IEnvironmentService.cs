namespace vrcosc_magicchatbox.Services;

public interface IEnvironmentService
{
    string DataPath { get; }

    string LogPath { get; }

    string VrcPath { get; }

    void SetCustomProfile(int profileNumber);
}
