using System.Collections.Generic;
using vrcosc_magicchatbox.Services.Hardware;
using Xunit;

namespace MagicChatbox.Tests.Services.Hardware;

public class GpuAdapterSelectorTests
{
    private const ulong Gib = 1UL << 30;
    private const ulong Mib = 1UL << 20;

    private sealed record FakeAdapter(
        string Name,
        uint AdapterIndex,
        uint? VendorId,
        ulong? DedicatedVideoMemoryBytes,
        bool IsSoftwareAdapter = false) : IGpuAdapter;

    private static FakeAdapter Rx6900Xt(uint index = 0) =>
        new("AMD Radeon RX 6900 XT", index, GpuVendors.Amd, 16 * Gib);

    private static FakeAdapter RyzenIgpu(uint index = 0) =>
        new("AMD Radeon(TM) Graphics", index, GpuVendors.Amd, 512 * Mib);

    private static FakeAdapter Rtx4080(uint index = 0) =>
        new("NVIDIA GeForce RTX 4080", index, GpuVendors.Nvidia, 16 * Gib);

    private static FakeAdapter IntelIgpu(uint index = 0) =>
        new("Intel(R) UHD Graphics 770", index, GpuVendors.Intel, 128 * Mib);

    private static FakeAdapter Warp(uint index = 0) =>
        new("Microsoft Basic Render Driver", index, GpuVendors.Microsoft, 0);


    [Theory]
    [InlineData(0x1002u, GpuVendor.Amd)]
    [InlineData(0x10DEu, GpuVendor.Nvidia)]
    [InlineData(0x8086u, GpuVendor.Intel)]
    [InlineData(0x1414u, GpuVendor.Microsoft)]
    [InlineData(0x1234u, GpuVendor.Unknown)]
    public void VendorIdMapsToVendor(uint vendorId, GpuVendor expected)
        => Assert.Equal(expected, GpuVendors.FromVendorId(vendorId));

    [Fact]
    public void NullVendorIdIsUnknown()
        => Assert.Equal(GpuVendor.Unknown, GpuVendors.FromVendorId(null));


    [Fact]
    public void SingleDiscreteCardIsSelected()
    {
        var selected = GpuAdapterSelector.SelectPrimary(new List<FakeAdapter> { Rx6900Xt() });

        Assert.Equal("AMD Radeon RX 6900 XT", selected?.Name);
    }

    [Fact]
    public void RyzenIgpuPlusRadeonDgpu_PicksTheDiscreteCard()
    {
        var adapters = new List<FakeAdapter> { RyzenIgpu(index: 0), Rx6900Xt(index: 1) };

        var selected = GpuAdapterSelector.SelectPrimary(adapters);

        Assert.Equal("AMD Radeon RX 6900 XT", selected?.Name);
    }

    [Fact]
    public void RyzenIgpuPlusRadeonDgpu_PicksTheDiscreteCardRegardlessOfEnumerationOrder()
    {
        var adapters = new List<FakeAdapter> { Rx6900Xt(index: 0), RyzenIgpu(index: 1) };

        Assert.Equal("AMD Radeon RX 6900 XT", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void IntelIgpuPlusNvidiaDgpu_PicksTheDiscreteCard()
    {
        var adapters = new List<FakeAdapter> { IntelIgpu(index: 0), Rtx4080(index: 1) };

        Assert.Equal("NVIDIA GeForce RTX 4080", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void SoftwareAdaptersAreIgnored()
    {
        var adapters = new List<FakeAdapter>
        {
            new("Software Rasterizer", 0, GpuVendors.Amd, 16 * Gib, IsSoftwareAdapter: true),
            Rx6900Xt(index: 1),
        };

        Assert.Equal("AMD Radeon RX 6900 XT", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void WarpIsIgnoredEvenWithoutTheSoftwareFlag()
    {
        var adapters = new List<FakeAdapter> { Warp(index: 0), IntelIgpu(index: 1) };

        Assert.Equal("Intel(R) UHD Graphics 770", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void WarpOnlyMachineSelectsNothing()
        => Assert.Null(GpuAdapterSelector.SelectPrimary(new List<FakeAdapter> { Warp() }));

    [Fact]
    public void BlankNamesAreIgnored()
    {
        var adapters = new List<FakeAdapter>
        {
            new("   ", 0, GpuVendors.Amd, 16 * Gib),
            RyzenIgpu(index: 1),
        };

        Assert.Equal("AMD Radeon(TM) Graphics", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void TwoIntegratedGpus_PrefersTheDiscreteVendorThenTheLargerOne()
    {
        var adapters = new List<FakeAdapter> { IntelIgpu(index: 0), RyzenIgpu(index: 1) };

        Assert.Equal("AMD Radeon(TM) Graphics", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void EnumerationOrderBreaksOtherwiseEqualTies()
    {
        var adapters = new List<FakeAdapter>
        {
            new("NVIDIA GeForce RTX 4080", 3, GpuVendors.Nvidia, 16 * Gib),
            new("NVIDIA GeForce RTX 4080", 1, GpuVendors.Nvidia, 16 * Gib),
        };

        Assert.Equal(1u, GpuAdapterSelector.SelectPrimary(adapters)?.AdapterIndex);
    }

    [Fact]
    public void UnknownVendorWithLotsOfVramStillBeatsAnIgpu()
    {
        var adapters = new List<FakeAdapter>
        {
            IntelIgpu(index: 0),
            new("Some Future GPU", 1, VendorId: null, 24 * Gib),
        };

        Assert.Equal("Some Future GPU", GpuAdapterSelector.SelectPrimary(adapters)?.Name);
    }

    [Fact]
    public void EmptyAndNullInputsSelectNothing()
    {
        Assert.Null(GpuAdapterSelector.SelectPrimary(new List<FakeAdapter>()));
        Assert.Null(GpuAdapterSelector.SelectPrimary<FakeAdapter>(null));
    }
}
