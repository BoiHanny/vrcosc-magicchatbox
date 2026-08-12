using System;
using System.IO;

namespace vrcosc_magicchatbox.Services;

public class EnvironmentService : IEnvironmentService
{
    private static readonly string DefaultDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Vrcosc-MagicChatbox");

    public string DataPath { get; private set; } = DefaultDataPath;

    public string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vrcosc-MagicChatbox", "logs");

    public string VrcPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Steam", "steamapps", "common", "VRChat");

    public void SetCustomProfile(int profileNumber)
    {
        DataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            $"Vrcosc-MagicChatbox-profile-{profileNumber}");
    }
}
