using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>Buffers and images carved out of shared blocks of device memory, on a real Vulkan device.</summary>
[Collection("E3D")]
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
            device.Read(buffers[i]).ToArray().Should().OnlyContain(b => b == (byte)i, $"buffer {i} keeps its own bytes");

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

    [NeedsVulkanFact]
    public void A_Buffer_The_CPU_Writes_Is_Mapped_Though_Its_Block_Was_Made_For_The_Devices_Own()
    {
        using var device = new GraphicsDevice();
        device.Initialize(new OffscreenSurface(16, 16), "memory test");

        // Where the device's own memory and the memory the CPU sees are one type, as on lavapipe and
        // a GPU sharing the computer's memory, a block made for a buffer of the device's own holds
        // the next that does not fit the blocks before it, whichever kind it is. Buffers of 30 MB
        // of the device's own are made until one takes a new block, so the blocks before hold less
        // than that free, and one of 32 MB the CPU writes, half a block and so carved from one,
        // fits the new block alone. Unmapped there, its first write through the null pointer ended
        // a voxel game's play on lavapipe.
        var own = new List<IBuffer>();
        try
        {
            var blocks = device.MemoryBlockCount;
            while (device.MemoryBlockCount == blocks && own.Count < 64)
                own.Add(device.CreateBuffer(new BufferDesc(30UL << 20, BufferUsage.Storage, CpuAccessMode.None)));
            own.Add(device.CreateBuffer(new BufferDesc(32UL << 20, BufferUsage.Storage, CpuAccessMode.Write)));
            device.Map(own[^1]).Fill(7);
            device.Read(own[^1])[^1].Should().Be(7, "the buffer the CPU writes is mapped wherever its range was carved");
        }
        finally
        {
            foreach (var buffer in own) buffer.Dispose();
        }
    }
}
