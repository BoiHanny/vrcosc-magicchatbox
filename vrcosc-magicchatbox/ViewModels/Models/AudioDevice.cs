namespace vrcosc_magicchatbox.ViewModels.Models
{
    /// <summary>
    /// Represents an audio output device with a friendly display name and system identifier.
    /// </summary>
    public class AudioDevice
    {
        public string FriendlyName { get; set; }

        public string ID { get; set; }

        public int DeviceNumber { get; set; }

        /// <summary>
        /// Initializes a new <see cref="AudioDevice"/> instance.
        /// </summary>
        public AudioDevice(string friendlyName, string id, int deviceNumber)
        {
            FriendlyName = friendlyName;
            ID = id;
            DeviceNumber = deviceNumber;
        }
    }
}
