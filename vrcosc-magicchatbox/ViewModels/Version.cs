namespace vrcosc_magicchatbox.ViewModels
{
    public class Version
    {
        public Version(string version)
        {
            VersionNumber = version;
            ReleaseDate = "";
            ReleaseNotes = "";
        }

        public string ReleaseDate { get; set; }

        public string ReleaseNotes { get; set; }

        public string VersionNumber { get; set; }
    }
}