using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>
/// A write-only camera or dolly action, named rather than addressed.
/// </summary>
/// <remarks>
/// An enum instead of a string, because <see cref="IVrcEgress"/>'s whole premise is that a caller names
/// an effect and never a destination — and "a string I validate against a table" is still a string a
/// caller composes. The architecture test caught exactly that when this took an address, which is what
/// the fence is for. With an enum, an address VRChat does not advertise is not merely rejected, it is
/// unrepresentable.
/// <para>
/// The dolly's import and export take a filesystem path and are deliberately absent. A path is not a
/// parameter value, and handing this interface one would reopen the same door from the other side.
/// </para>
/// </remarks>
public enum VrcAction : byte
{
    /// <summary>Close the camera. Equivalent to setting its mode to 0.</summary>
    CameraClose,

    /// <summary>Take a photo.</summary>
    CameraCapture,

    /// <summary>Take a timed photo.</summary>
    CameraCaptureDelayed,

    /// <summary>Play a dolly animation after a delay, in seconds.</summary>
    DollyPlayDelayed,
}

/// <summary>What VRChat will let you do with one of its non-avatar addresses.</summary>
public enum VrcAccess : byte
{
    /// <summary>VRChat reports it; writes are ignored.</summary>
    Read,

    /// <summary>VRChat accepts it and never reports it. No cell can honestly assert a current value.</summary>
    Write,

    /// <summary>Both. This is the interesting case, and the reason the <c>vrc</c> namespace exists.</summary>
    ReadWrite,
}

/// <summary>
/// One address of a VRChat subsystem that is not the avatar.
/// </summary>
/// <param name="Address">The OSC address, verbatim.</param>
/// <param name="Key">The kernel key it projects onto, or null for write-only addresses.</param>
/// <param name="Kind">What the value is.</param>
/// <param name="Access">Which directions VRChat supports.</param>
/// <param name="Min">The documented lower bound, when VRChat states one.</param>
/// <param name="Max">The documented upper bound, when VRChat states one.</param>
/// <param name="Default">The documented default, when VRChat states one.</param>
/// <param name="Description">One human sentence, for the UI and the assistant's tool schema.</param>
public readonly record struct VrcSubsystemAddress(
    string Address,
    string? Key,
    SignalKind Kind,
    VrcAccess Access,
    double? Min,
    double? Max,
    double? Default,
    string Description);

/// <summary>
/// VRChat's camera and dolly, as data.
/// </summary>
/// <remarks>
/// <para>
/// A table rather than a switch, because every consumer wants a different slice of the same facts:
/// ingress needs address → key, the schema needs the bounds, egress needs address → type tag, and a UI
/// needs all of it plus the sentence. Writing it five times is how they drift.
/// </para>
/// <para>
/// <b>The bounds are VRChat's, not invented.</b> Every Min/Max/Default below is quoted from VRChat's own
/// documentation, which is the only reason this file can state them at all — OSCQuery advertises a
/// RANGE attribute but nothing here reads it, and a made-up slider bound is worse than none.
/// </para>
/// <para>
/// One oddity is preserved rather than corrected: <c>Lightness</c> is documented with a default of 60
/// and a maximum of 50. That is what the source says. Silently clamping the default into the range
/// would hide either a documentation error or a real quirk, and this table's job is to report what
/// VRChat claims.
/// </para>
/// </remarks>
public static class VrcSubsystems
{
    /// <summary>Every camera and dolly address, in one list.</summary>
    public static readonly IReadOnlyList<VrcSubsystemAddress> All = BuildAll();

    /// <summary>The addresses that carry readable state, and therefore become cells.</summary>
    public static IEnumerable<VrcSubsystemAddress> Readable =>
        All.Where(a => a.Access != VrcAccess.Write && a.Key is not null);

    /// <summary>Finds the address a kernel key came from, for egress.</summary>
    public static bool TryByKey(string key, out VrcSubsystemAddress address) =>
        ByKey.TryGetValue(key, out address);

    /// <summary>Finds what an incoming OSC address is, for ingress.</summary>
    public static bool TryByAddress(string address, out VrcSubsystemAddress descriptor) =>
        ByAddress.TryGetValue(address, out descriptor);

    /// <summary>The address and payload kind one named action maps onto.</summary>
    public static VrcSubsystemAddress For(VrcAction action) => ByAddress[action switch
    {
        VrcAction.CameraClose => "/usercamera/Close",
        VrcAction.CameraCapture => "/usercamera/Capture",
        VrcAction.CameraCaptureDelayed => "/usercamera/CaptureDelayed",
        VrcAction.DollyPlayDelayed => "/dolly/PlayDelayed",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    }];

    private static readonly Dictionary<string, VrcSubsystemAddress> ByAddress =
        All.ToDictionary(a => a.Address, StringComparer.Ordinal);

    private static readonly Dictionary<string, VrcSubsystemAddress> ByKey =
        All.Where(a => a.Key is not null).ToDictionary(a => a.Key!, StringComparer.Ordinal);

    private static IReadOnlyList<VrcSubsystemAddress> BuildAll()
    {
        var all = new List<VrcSubsystemAddress>();

        // Camera mode and pose.
        all.Add(new("/usercamera/Mode", "vrc.camera.mode", SignalKind.Int, VrcAccess.ReadWrite,
            0, 6, null, "0 off, 1 photo, 2 stream, 3 emoji, 4 multilayer, 5 print, 6 drone."));

        // Pose is ffffff and read-only, so it becomes six cells like the tracked poses do. It is listed
        // here for completeness and handled by the pose projection rather than as a single value.
        all.Add(new("/usercamera/Pose", null, SignalKind.Float, VrcAccess.Read,
            null, null, null, "Camera position and rotation. Six floats, projected as six keys."));

        // Actions. Write-only momentary buttons: no cell, because nothing echoes them.
        Action("/usercamera/Close", "Close the camera.");
        Action("/usercamera/Capture", "Take a photo.");
        Action("/usercamera/CaptureDelayed", "Take a timed photo.");

        // Toggles. All read/write, so all real state.
        Toggle("ShowUIInCamera", "Include the UI in the shot.");
        Toggle("LocalPlayer", "Include yourself.");
        Toggle("RemotePlayer", "Include other players.");
        Toggle("Environment", "Include the world.");
        Toggle("GreenScreen", "Green screen background.");
        Toggle("Lock", "Lock the camera in place.");
        Toggle("SmoothMovement", "Smooth the camera's motion.");
        Toggle("LookAtMe", "Point the camera at you.");
        Toggle("AutoLevelRoll", "Keep the horizon level.");
        Toggle("AutoLevelPitch", "Keep the pitch level.");
        Toggle("Flying", "Let the camera fly.");
        Toggle("TriggerTakesPhotos", "Trigger takes photos.");
        Toggle("DollyPathsStayVisible", "Keep dolly paths visible while animating.");
        Toggle("AudioFromCamera", "Record audio from the camera's position.");
        Toggle("ShowFocus", "Show the focus overlay.");
        Toggle("Streaming", "Stream over Spout.");
        Toggle("RollWhileFlying", "Allow roll while flying.");
        Toggle("OrientationIsLandscape", "Landscape rather than portrait.");

        // Sliders, with VRChat's documented default/min/max.
        Slider("Zoom", 20, 150, 45, "Field of view.");
        Slider("Exposure", -10, 4, 0, "Exposure compensation.");
        Slider("FocalDistance", 0, 10, 1.5, "Focal distance.");
        Slider("Aperture", 1.4, 32, 15, "Aperture.");
        Slider("Hue", 0, 360, 120, "Green screen hue.");
        Slider("Saturation", 0, 100, 100, "Green screen saturation.");
        Slider("Lightness", 0, 50, 60, "Green screen lightness. VRChat documents a default above its own maximum.");
        Slider("LookAtMeXOffset", -25, 25, 0, "Look-at-me horizontal offset.");
        Slider("LookAtMeYOffset", -25, 25, 0, "Look-at-me vertical offset.");
        Slider("FlySpeed", 0.1, 15, 3, "Fly speed.");
        Slider("TurnSpeed", 0.1, 5, 1, "Turn speed.");
        Slider("SmoothingStrength", 0.1, 10, 5, "Smoothing strength.");
        Slider("PhotoRate", 0.1, 2, 1, "Dolly photo capture rate.");
        Slider("Duration", 0.1, 60, 2, "Dolly duration.");

        // Dolly. Play is the only readable one; the rest take a path and report nothing.
        all.Add(new("/dolly/Play", "vrc.dolly.playing", SignalKind.Bool, VrcAccess.ReadWrite,
            null, null, null, "Whether a dolly animation is playing."));
        all.Add(new("/dolly/PlayDelayed", null, SignalKind.Float, VrcAccess.Write,
            null, null, null, "Play after a delay, in seconds."));
        all.Add(new("/dolly/Import", null, SignalKind.Text, VrcAccess.Write,
            null, null, null, "Import a dolly path from a JSON file."));
        all.Add(new("/dolly/Export", null, SignalKind.Text, VrcAccess.Write,
            null, null, null, "Export the current dolly path to a JSON file."));
        all.Add(new("/dolly/ExportLocal", null, SignalKind.Text, VrcAccess.Write,
            null, null, null, "Export the dolly path with all points local."));

        return all;

        void Action(string address, string description) =>
            all.Add(new(address, null, SignalKind.Bool, VrcAccess.Write, null, null, null, description));

        void Toggle(string name, string description) => all.Add(new(
            $"/usercamera/{name}",
            $"vrc.camera.{name.ToLowerInvariant()}",
            SignalKind.Bool,
            VrcAccess.ReadWrite,
            null, null, null,
            description));

        void Slider(string name, double min, double max, double fallback, string description) => all.Add(new(
            $"/usercamera/{name}",
            $"vrc.camera.{name.ToLowerInvariant()}",
            SignalKind.Float,
            VrcAccess.ReadWrite,
            min, max, fallback,
            description));
    }
}
