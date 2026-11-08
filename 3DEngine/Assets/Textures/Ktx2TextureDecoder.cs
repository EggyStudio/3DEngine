using System.Buffers.Binary;

namespace Engine;

/// <summary>
/// Reads a KTX2 file of the blocks a GPU samples without unpacking, BC1, BC5 or BC7, every mip level
/// it holds, first to last, as the GPU takes them.
/// </summary>
/// <remarks>
/// A level supercompressed with zstd is refused with the reason, since .NET's library holds no
/// zstd; build/make-scene-pack.py writes such a pack's files again with their levels as they are. A
/// BC1 or BC7 file of an sRGB format is a color's, its blocks sampled decoded to linear, and every
/// other's linear.
/// </remarks>
internal static class Ktx2
{
    private static ReadOnlySpan<byte> Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A KTX2 file's levels, or throws with why it cannot be read.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a KTX2 file of BC1, BC5 or BC7 levels as they are.</exception>
    public static TextureAsset Read(ReadOnlySpan<byte> file, string path)
    {
        if (file.Length < 80 || !file[..12].SequenceEqual(Identifier))
            throw new InvalidDataException($"'{path}' is not a KTX2 file.");
        var vkFormat = BinaryPrimitives.ReadUInt32LittleEndian(file[12..]);
        var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(file[20..]);
        var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(file[24..]);
        var levels = Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(file[40..]));
        var scheme = BinaryPrimitives.ReadUInt32LittleEndian(file[44..]);
        if (scheme != 0)
            throw new InvalidDataException($"'{path}' has its levels supercompressed (scheme {scheme}), which this reads none of. " +
                                           "build/make-scene-pack.py writes a file's levels again as they are.");
        var (format, srgb) = vkFormat switch
        {
            131 or 133 => (TextureFormat.Bc1, false),
            132 or 134 => (TextureFormat.Bc1, true),
            141 => (TextureFormat.Bc5, false),
            145 => (TextureFormat.Bc7, false),
            146 => (TextureFormat.Bc7, true),
            _ => throw new InvalidDataException($"'{path}' holds Vulkan format {vkFormat}, where a KTX2 file read here holds BC1, BC5 or BC7."),
        };
        if (width <= 0 || height <= 0 || file.Length < 80 + 24 * levels)
            throw new InvalidDataException($"'{path}' gives no size or no level index.");

        var blockBytes = format == TextureFormat.Bc1 ? 8 : 16;
        using var pixels = new MemoryStream();
        for (int level = 0; level < levels; level++)
        {
            var entry = file[(80 + 24 * level)..];
            var (offset, length) = (BinaryPrimitives.ReadUInt64LittleEndian(entry), BinaryPrimitives.ReadUInt64LittleEndian(entry[8..]));
            int w = Math.Max(1, width >> level), h = Math.Max(1, height >> level);
            var expected = (ulong)((w + 3) / 4 * ((h + 3) / 4) * blockBytes);
            if (length != expected || offset + length > (ulong)file.Length)
                throw new InvalidDataException($"'{path}' level {level} holds {length} bytes at {offset}, where its blocks are {expected}.");
            pixels.Write(file.Slice((int)offset, (int)length));
        }
        return new TextureAsset
        {
            Pixels = pixels.ToArray(),
            Width = width,
            Height = height,
            MipCount = levels,
            Format = format,
            ColorSpace = srgb ? TextureColorSpace.Srgb : TextureColorSpace.Linear,
            SourcePath = path,
            SourceFormat = "ktx2",
        };
    }

    /// <summary>The bytes each level of a texture of blocks holds, first to last.</summary>
    public static IReadOnlyList<ReadOnlyMemory<byte>> Levels(TextureAsset texture)
    {
        var blockBytes = TextureFormatInfo.BytesPerBlock(texture.Format);
        var levels = new ReadOnlyMemory<byte>[texture.MipCount];
        var at = 0;
        for (int level = 0; level < levels.Length; level++)
        {
            int w = Math.Max(1, texture.Width >> level), h = Math.Max(1, texture.Height >> level);
            var bytes = (w + 3) / 4 * ((h + 3) / 4) * blockBytes;
            levels[level] = texture.Pixels.AsMemory(at, bytes);
            at += bytes;
        }
        return levels;
    }
}

/// <summary><see cref="ITextureDecoder"/> of KTX2 files of BC1, BC5 or BC7 blocks (<see cref="Ktx2"/>).</summary>
internal sealed class Ktx2TextureDecoder : ITextureDecoder
{
    /// <inheritdoc />
    public string[] Extensions => [".ktx2"];

    /// <inheritdoc />
    public string FormatId => "ktx2";

    /// <inheritdoc />
    public Task<TextureAsset> DecodeAsync(AssetLoadContext context, TextureLoadSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var stream = context.GetStream();
        if (stream.CanSeek) stream.Position = 0;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = Ktx2.Read(bytes.GetBuffer().AsSpan(0, (int)bytes.Length), context.Path.ToString());
        return Task.FromResult(settings.ColorSpace is { } space && space != texture.ColorSpace
            ? new TextureAsset
            {
                Pixels = texture.Pixels, Width = texture.Width, Height = texture.Height, MipCount = texture.MipCount, Format = texture.Format,
                ColorSpace = space, SourcePath = texture.SourcePath, SourceFormat = texture.SourceFormat,
            }
            : texture);
    }
}
