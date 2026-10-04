using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>Buffers and images carved out of shared blocks of device memory, on a real Vulkan device.</summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class DeviceMemoryTests
{
    [NeedsVulkanFact]
    public void Ten_Thousand_Buffers_And_Three_Thousand_Textures_Take_A_Few_Allocations()
    {
        using var device = new GraphicsDevice();
        device.Initialize(new OffscreenSurface(16, 16), "memory test");

        // Many drivers refuse an allocation past 4,096, which these would pass at one each.
        var buffers = Enumerable.Range(0, 10_000)
            .Select(_ => device.CreateBuffer(new BufferDesc(256, BufferUsage.Vertex, CpuAccessMode.Write)))
            .ToList();
        var images = Enumerable.Range(0, 3_000)
            .Select(_ => device.CreateImage(new ImageDesc(new Extent2D(8, 8), ImageFormat.R8G8B8A8_UNorm, ImageUsage.Sampled | ImageUsage.TransferDst)))
            .ToList();
        device.MemoryBlockCount.Should().BeLessThan(20);

        // Each buffer's mapped range is its own, so what one holds survives the others' writes.
        for (int i = 0; i < buffers.Count; i++) device.Map(buffers[i]).Fill((byte)i);
        for (int i = 0; i < buffers.Count; i++)
            device.Map(buffers[i]).ToArray().Should().OnlyContain(b => b == (byte)i, $"buffer {i} keeps its own bytes");

        // Freed ranges are taken again, so the blocks do not grow as buffers come and go.
        var before = device.MemoryBlockCount;
        foreach (var buffer in buffers.Take(5_000)) buffer.Dispose();
        var again = Enumerable.Range(0, 5_000)
            .Select(_ => device.CreateBuffer(new BufferDesc(256, BufferUsage.Vertex, CpuAccessMode.Write)))
            .ToList();
        device.MemoryBlockCount.Should().Be(before);

        foreach (var buffer in buffers.Skip(5_000).Concat(again)) buffer.Dispose();
        foreach (var image in images) image.Dispose();
    }
}
