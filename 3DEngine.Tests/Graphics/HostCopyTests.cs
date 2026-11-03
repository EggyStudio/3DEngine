using FluentAssertions;

namespace Engine.Tests.Graphics;

/// <summary>Textures the CPU copies into their images, against the same through a staging buffer, on a real Vulkan device.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class HostCopyTests
{
    [NeedsVulkanFact]
    public void A_Texture_Copied_In_By_The_CPU_Holds_The_Texels_A_Staging_Buffer_Gives_It_At_Every_Level()
    {
        // A BC7 texture of 64 by 64 with its seven levels given, as a KTX2 file holds them, and one
        // of four bytes a pixel whose levels the GPU blits from the first, each made on a device
        // that copies into them from the CPU where it can and on one made to stage every upload.
        var random = new Random(182);
        var blocks = Enumerable.Range(0, 7).Select(level => (ReadOnlyMemory<byte>)RandomBytes(random, Math.Max(1, 16 >> level) * Math.Max(1, 16 >> level) * 16)).ToList();
        var rgba = RandomBytes(random, 64 * 64 * 4);
        (List<byte[]> Bc7, List<byte[]> Rgba, bool OnHost) Read(bool staged)
        {
            using var device = new GraphicsDevice { StagesUploads = staged };
            device.Initialize(new OffscreenSurface(16, 16), "host copy test");
            using var compressed = device.CreateCompressedTexture(ImageFormat.Bc7_UNorm, 64, 64, blocks);
            using var pixels = device.CreateImage(new ImageDesc(new Extent2D(64, 64), ImageFormat.R8G8B8A8_UNorm,
                ImageUsage.Sampled | ImageUsage.TransferDst | ImageUsage.TransferSrc | ImageUsage.Storage, ImageDesc.FullMipChain(64, 64)));
            device.UploadTexture2D(pixels, rgba, 64, 64, 4);
            return ([.. Enumerable.Range(0, 7).Select(level => device.DecodedPixels(compressed, (uint)level)!)],
                [.. Enumerable.Range(0, 7).Select(level => device.DecodedPixels(pixels, (uint)level)!)], device.CopiesOnHost);
        }
        var staged = Read(staged: true);
        var copied = Read(staged: false);
        staged.OnHost.Should().BeFalse("a device made to stage its uploads copies none from the CPU");
        for (int level = 0; level < 7; level++)
        {
            copied.Bc7[level].Should().Equal(staged.Bc7[level], $"the BC7 texture's level {level} holds the same texels, copied in by the CPU: {copied.OnHost}");
            copied.Rgba[level].Should().Equal(staged.Rgba[level], $"the four-byte texture's level {level} holds the same texels, copied in by the CPU: {copied.OnHost}");
        }
    }

    private static byte[] RandomBytes(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }
}
