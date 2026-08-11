using System;
using vrcosc_magicchatbox.Services.Hardware;
using Xunit;

namespace MagicChatbox.Tests.Services.Hardware;

/// <summary>
/// Hardware-dependent smoke tests. They assert lifecycle safety rather than specific readings, so
/// they pass on a build agent with no GPU as well as on a real machine. Actual sensor values are
/// verified manually — see the plan's verification section.
/// </summary>
public class LhmGpuSensorProviderTests
{
    [Fact]
    public void OpenAndCloseAreSafeAndRepeatable()
    {
        using var provider = new LhmGpuSensorProvider();

        provider.TryOpen();
        provider.TryOpen();
        provider.Close();
        provider.Close();
        provider.TryOpen();

        // Whether a GPU was found is machine-dependent; not throwing is not.
        Assert.NotNull(provider.DescribeStatus());
    }

    [Fact]
    public void ReadBeforeOpenReturnsNullRatherThanThrowing()
    {
        using var provider = new LhmGpuSensorProvider();

        Assert.Null(provider.Read("Anything"));
        Assert.False(provider.IsOpen);
    }

    [Fact]
    public void ReadAfterDisposeReturnsNullRatherThanThrowing()
    {
        var provider = new LhmGpuSensorProvider();
        provider.TryOpen();
        provider.Dispose();

        Assert.Null(provider.Read(null));
        Assert.False(provider.IsOpen);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var provider = new LhmGpuSensorProvider();
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public void AnUnknownGpuNameDoesNotBorrowAnotherAdaptersReadings()
    {
        using var provider = new LhmGpuSensorProvider();
        provider.TryOpen();

        var readings = provider.Read("Definitely Not A Real GPU 9999");

        // An explicit name that matches nothing must resolve to nothing, however many GPUs are
        // present. Falling back to the only visible adapter is how a selected iGPU ends up
        // reporting the dGPU's hotspot temperature and board power.
        Assert.Null(readings);
    }

    [Fact]
    public void NoRequestedNameStillFallsBackToASingleGpu()
    {
        using var provider = new LhmGpuSensorProvider();
        if (!provider.TryOpen())
            return;

        // The fallback is only correct when the caller expressed no preference.
        if (provider.GetHardwareNames().Count == 1)
            Assert.NotNull(provider.Read(null));
    }

    [Fact]
    public void ReportedGpusAreNamedAndVendorTagged()
    {
        using var provider = new LhmGpuSensorProvider();
        if (!provider.TryOpen())
            return;

        foreach (string name in provider.GetHardwareNames())
        {
            var readings = provider.Read(name);
            Assert.NotNull(readings);
            Assert.False(string.IsNullOrWhiteSpace(readings!.HardwareName));
            Assert.NotEqual(GpuVendor.Unknown, readings.Vendor);
            Assert.NotEqual(GpuVendor.Microsoft, readings.Vendor);
        }
    }

    [Fact]
    public void PercentageSensorsStayInRange()
    {
        using var provider = new LhmGpuSensorProvider();
        if (!provider.TryOpen())
            return;

        foreach (string name in provider.GetHardwareNames())
        {
            var r = provider.Read(name);
            if (r == null)
                continue;

            foreach (float? percent in new[] { r.CoreLoad, r.D3DLoad, r.MemoryLoad, r.FanPercent })
            {
                if (percent is { } value)
                    Assert.InRange(value, 0f, 100f);
            }
        }
    }
}
