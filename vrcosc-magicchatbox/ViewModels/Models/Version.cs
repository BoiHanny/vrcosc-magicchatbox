using System;
using System.Linq;

namespace vrcosc_magicchatbox.ViewModels.Models
{
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
                // If parts are less than 3, pad missing parts with zeros
                Array.Resize(ref parts, 3);
            }

            parts[0] = "0"; // Always set the first part to 0
            parts[1] = int.Parse(parts[1]).ToString(); // Ensure the middle part is an integer
            parts[2] = int.Parse(parts[2]).ToString().PadLeft(3, '0'); // Ensure the last part is three digits, padding with zeros if necessary

            return string.Join(".", parts);
        }

    }
}
