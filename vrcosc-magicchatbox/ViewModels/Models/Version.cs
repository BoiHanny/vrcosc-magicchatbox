using System;
using System.Linq;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    /// <summary>
    /// Normalizes an application version string to the <c>0.MINOR.BUILD</c> format used
    /// for display and comparison across the app.
    /// </summary>
    public class Version
    {
        public Version(string version)
        {
            VersionNumber = EnsureCorrectFormat(version);
            ReleaseDate = "";
            ReleaseNotes = "";
        }

        public string ReleaseDate { get; set; }

        public string ReleaseNotes { get; set; }

        private string _versionNumber;

        public string VersionNumber
        {
            get => _versionNumber;
            set => _versionNumber = EnsureCorrectFormat(value);
        }

        private string EnsureCorrectFormat(string version)
        {
            var parts = version.Split('.');

            // Ensure we only process the first three parts (major, minor, build)
            if (parts.Length > 3)
            {
                // Ignore the revision part (typically the 4th part)
                parts = parts.Take(3).ToArray();
            }

            if (parts.Length < 3)
            {
                Array.Resize(ref parts, 3);
            }

            parts[0] = "0"; // Always set the first part to 0
            parts[1] = int.Parse(parts[1]).ToString();
            parts[2] = int.Parse(parts[2]).ToString().PadLeft(3, '0');

            return string.Join(".", parts);
        }

    }
}
