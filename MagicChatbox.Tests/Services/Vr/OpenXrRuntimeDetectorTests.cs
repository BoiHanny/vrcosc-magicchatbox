using vrcosc_magicchatbox.Services.Vr;
using Xunit;

namespace MagicChatbox.Tests.Services.Vr;

public class OpenXrRuntimeDetectorTests
{
    private const string VdxrManifest = """
        { "file_format_version": "1.0.0",
          "runtime": { "library_path": ".\\virtualdesktop-openxr.dll", "name": "VirtualDesktopXR (Bundled)" } }
        """;

    private const string SteamVrManifest = """
        { "file_format_version": "1.0.0",
          "runtime": { "library_path": "..\\..\\bin\\win64\\vrclient_x64.dll", "name": "SteamVR" } }
        """;

    [Fact]
    public void VdxrIsRecognisedAndReportedAsUnsupported()
    {
        var info = OpenXrRuntimeDetector.Classify(
            @"C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json",
            VdxrManifest);

        Assert.Equal(XrRuntimeKind.VirtualDesktopXr, info.Kind);
        Assert.Equal("VirtualDesktopXR (Bundled)", info.Name);
        Assert.False(info.SupportsFrameTiming);
        Assert.Contains("Virtual Desktop", info.DescribeForUser());
        Assert.Contains("SteamVR", info.DescribeForUser());
    }

    [Fact]
    public void SteamVrIsTheOnlyRuntimeThatSupportsFrameTiming()
    {
        var info = OpenXrRuntimeDetector.Classify(
            @"G:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json",
            SteamVrManifest);

        Assert.Equal(XrRuntimeKind.SteamVr, info.Kind);
        Assert.True(info.SupportsFrameTiming);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json", XrRuntimeKind.Oculus)]
    [InlineData(@"C:\WINDOWS\system32\MixedRealityRuntime.json", XrRuntimeKind.WindowsMixedReality)]
    [InlineData(@"C:\Some\Vendor\weird_runtime.json", XrRuntimeKind.Other)]
    public void OtherRuntimesAreClassifiedFromThePathWhenTheManifestIsUnreadable(string path, XrRuntimeKind expected)
    {
        var info = OpenXrRuntimeDetector.Classify(path, manifestJson: null);

        Assert.Equal(expected, info.Kind);
        Assert.False(info.SupportsFrameTiming);
    }

    [Fact]
    public void TheManifestNameWinsOverAMisleadingPath()
    {
        var info = OpenXrRuntimeDetector.Classify(@"D:\games\stuff\runtime.json", SteamVrManifest);

        Assert.Equal(XrRuntimeKind.SteamVr, info.Kind);
    }

    [Fact]
    public void NoRegisteredRuntimeIsUnknownRatherThanACrash()
    {
        var info = OpenXrRuntimeDetector.Classify(null, null);

        Assert.Equal(XrRuntimeKind.Unknown, info.Kind);
        Assert.False(info.SupportsFrameTiming);
        Assert.Contains("No OpenXR runtime", info.DescribeForUser());
    }

    [Fact]
    public void MalformedManifestFallsBackToThePathInsteadOfThrowing()
    {
        var info = OpenXrRuntimeDetector.Classify(
            @"C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json",
            "{ this is not valid json at all");

        Assert.Equal(XrRuntimeKind.VirtualDesktopXr, info.Kind);
    }

    [Fact]
    public void DetectOnThisMachineDoesNotThrow()
    {
        var info = OpenXrRuntimeDetector.Detect();
        Assert.NotNull(info);
        Assert.NotNull(info.DescribeForUser());
    }
}
