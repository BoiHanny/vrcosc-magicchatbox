using vrcosc_magicchatbox.Services.Hardware;
using Xunit;

namespace MagicChatbox.Tests.Services.Hardware;

public class WmiVendorIdTests
{
    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2704&SUBSYS_51111462&REV_A1\4&D7CE646&0&0009", GpuVendors.Nvidia)]
    [InlineData(@"PCI\VEN_1002&DEV_164E&SUBSYS_7D701462&REV_C9\4&1E51CAC1&0&0041", GpuVendors.Amd)]
    [InlineData(@"PCI\VEN_8086&DEV_4680", GpuVendors.Intel)]
    [InlineData(@"pci\ven_10de&dev_2704", GpuVendors.Nvidia)]
    public void PnpDeviceIdVendorTokenWins(string pnpDeviceId, uint expected)
    {
        Assert.Equal(expected, GpuVendors.ParseWmiVendorId(pnpDeviceId, null));
    }

    [Theory]
    [InlineData("NVIDIA", GpuVendors.Nvidia)]
    [InlineData("Advanced Micro Devices, Inc.", GpuVendors.Amd)]
    [InlineData("Intel Corporation", GpuVendors.Intel)]
    public void AdapterCompatibilityIsTheFallback(string compatibility, uint expected)
    {
        Assert.Equal(expected, GpuVendors.ParseWmiVendorId(null, compatibility));
    }

    [Fact]
    public void RealNvidiaAdapterResolvesToNvidiaVendor()
    {
        uint? vendorId = GpuVendors.ParseWmiVendorId(
            @"PCI\VEN_10DE&DEV_2704&SUBSYS_51111462&REV_A1\4&D7CE646&0&0009", "NVIDIA");

        Assert.Equal(GpuVendor.Nvidia, GpuVendors.FromVendorId(vendorId));
    }

    [Fact]
    public void NonPciAdapterWithNoRecognisableVendorStaysNull()
    {
        Assert.Null(GpuVendors.ParseWmiVendorId(@"ROOT\DISPLAY\0000", "Virtual Desktop, Inc."));
        Assert.Null(GpuVendors.ParseWmiVendorId(null, null));
    }

    [Fact]
    public void BasicRenderDriverIsRecognisedSoItCanBeExcluded()
    {
        uint? vendorId = GpuVendors.ParseWmiVendorId(null, "Microsoft");
        Assert.Equal(GpuVendor.Microsoft, GpuVendors.FromVendorId(vendorId));
    }
}
