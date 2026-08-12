using System;
using vrcosc_magicchatbox.Services.Hardware;
using Xunit;

namespace MagicChatbox.Tests.Services.Hardware;

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

        Assert.Null(readings);
    }

    [Fact]
    public void NoRequestedNameStillFallsBackToASingleGpu()
    {
        using var provider = new LhmGpuSensorProvider();
        if (!provider.TryOpen())
            return;

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
