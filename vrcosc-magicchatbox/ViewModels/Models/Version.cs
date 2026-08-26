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

        private string _versionNumber = string.Empty;

        public string VersionNumber
        {
            get => _versionNumber;
            set => _versionNumber = EnsureCorrectFormat(value);
        }

        private string EnsureCorrectFormat(string version)
        {
            var parts = (version ?? string.Empty).Split('.');

            if (parts.Length > 3)
            {
                parts = parts.Take(3).ToArray();
            }

            if (parts.Length < 3)
            {
                Array.Resize(ref parts, 3);
            }

            // A tag is not obliged to carry three segments, and one that does not used to take
            // the version check down with it rather than reading as the zero it means.
            parts[0] = "0";
            parts[1] = SegmentOrZero(parts[1]).ToString();
            parts[2] = SegmentOrZero(parts[2]).ToString().PadLeft(3, '0');

            return string.Join(".", parts);
        }

        private static int SegmentOrZero(string segment)
            => int.TryParse(segment, out int value) ? value : 0;

    }
}
