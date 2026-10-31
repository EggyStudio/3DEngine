using System.Buffers.Binary;
using FluentAssertions;

namespace Engine.Tests.Assets.Textures;

/// <summary>KTX2 files of solid blocks for the tests, each level one color all over.</summary>
internal static class Ktx2Files
{
    /// <summary>
    /// A KTX2 file of Vulkan format <paramref name="vkFormat"/>, <paramref name="size"/> texels a
    /// side, a level for each halving down to one texel or the first <paramref name="count"/>, every
    /// block <paramref name="block"/>, its levels supercompressed by <paramref name="scheme"/> as the
    /// header says, though they are not.
    /// </summary>
    public static byte[] Solid(uint vkFormat, int size, byte[] block, uint scheme = 0, int? count = null)
    {
        var levels = count ?? System.Numerics.BitOperations.Log2((uint)size) + 1;
        var data = new List<byte[]>();
        for (int level = 0; level < levels; level++)
        {
            var side = Math.Max(1, (size >> level + 2) + ((size >> level) % 4 == 0 ? 0 : 1));
            data.Add([.. Enumerable.Repeat(block, side * side).SelectMany(b => b)]);
        }
        var head = 80 + 24 * levels;
        var places = new int[levels];
        var end = head;
        for (int level = levels - 1; level >= 0; level--)
        {
            end = (end + 15) / 16 * 16;
            places[level] = end;
            end += data[level].Length;
        }
        var file = new byte[end];
        byte[] identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];
        identifier.CopyTo(file, 0);
        uint[] header = [vkFormat, 1, (uint)size, (uint)size, 0, 0, 1, (uint)levels, scheme];
        for (int i = 0; i < header.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12 + 4 * i), header[i]);
        for (int level = 0; level < levels; level++)
        {
            var entry = file.AsSpan(80 + 24 * level);
            BinaryPrimitives.WriteUInt64LittleEndian(entry, (ulong)places[level]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[8..], (ulong)data[level].Length);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[16..], (ulong)data[level].Length);
            data[level].CopyTo(file, places[level]);
        }
        return file;
    }

    /// <summary>A BC1 block of one color, both ends the color in 5, 6 and 5 bits and every index the first.</summary>
    public static byte[] Bc1(int r, int g, int b)
    {
        var color = (ushort)((r >> 3) << 11 | (g >> 2) << 5 | (b >> 3));
        var block = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(block, color);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(2), color);
        return block;
    }

    /// <summary>
    /// A BC7 block of mode 6 of one color, both ends the color, every channel odd, since the ends'
    /// channels share their lowest bit, and every index the first.
    /// </summary>
    public static byte[] Bc7(int r, int g, int b, int a)
    {
        UInt128 bits = 1u << 6;
        var at = 7;
        foreach (var channel in new[] { r, g, b, a })
        {
            if ((channel & 1) == 0) throw new ArgumentException("Each channel of a mode 6 block of one color here is odd.");
            bits |= (UInt128)(uint)(channel >> 1) << at;
            bits |= (UInt128)(uint)(channel >> 1) << at + 7;
            at += 14;
        }
        bits |= (UInt128)1 << 63;
        bits |= (UInt128)1 << 64;
        var block = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(block, bits);
        return block;
    }

    /// <summary>A BC5 block of one value in each of its two channels.</summary>
    public static byte[] Bc5(int x, int y) => [(byte)x, (byte)x, 0, 0, 0, 0, 0, 0, (byte)y, (byte)y, 0, 0, 0, 0, 0, 0];
}

public sealed class Ktx2Tests
{
    [Fact]
    public void A_File_Of_BC7_Blocks_Reads_Every_Level_From_The_First_And_Its_SRGB_Format_As_A_Color()
    {
        var texture = Ktx2.Read(Ktx2Files.Solid(146, 16, Ktx2Files.Bc7(1, 201, 1, 255)), "color.ktx2");
        texture.Format.Should().Be(TextureFormat.Bc7);
        texture.ColorSpace.Should().Be(TextureColorSpace.Srgb);
        (texture.Width, texture.Height, texture.MipCount).Should().Be((16, 16, 5));
        Ktx2.Levels(texture).Select(level => level.Length).Should().Equal([16 * 16, 4 * 16, 16, 16, 16], "a level's blocks cover it whole, one block for each level of four texels or fewer");
        texture.Pixels.Length.Should().Be(16 * 16 + 4 * 16 + 3 * 16);
    }

    [Fact]
    public void BC1_And_BC5_Files_Read_As_Their_Formats_Linear()
    {
        var bc1 = Ktx2.Read(Ktx2Files.Solid(131, 8, Ktx2Files.Bc1(255, 0, 0)), "rough.ktx2");
        (bc1.Format, bc1.ColorSpace, bc1.MipCount).Should().Be((TextureFormat.Bc1, TextureColorSpace.Linear, 4));
        Ktx2.Levels(bc1)[0].Length.Should().Be(4 * 8, "four blocks of 8 bytes");
        var bc5 = Ktx2.Read(Ktx2Files.Solid(141, 4, Ktx2Files.Bc5(128, 128)), "normal.ktx2");
        (bc5.Format, bc5.ColorSpace, bc5.MipCount).Should().Be((TextureFormat.Bc5, TextureColorSpace.Linear, 3));
    }

    [Fact]
    public void A_Supercompressed_File_Or_One_Of_Another_Format_Is_Refused_With_Why()
    {
        var zstd = () => Ktx2.Read(Ktx2Files.Solid(146, 4, Ktx2Files.Bc7(1, 1, 1, 1), scheme: 2), "zstd.ktx2");
        zstd.Should().Throw<InvalidDataException>().WithMessage("*supercompressed*build/make-scene-pack.py*");
        var astc = () => Ktx2.Read(Ktx2Files.Solid(157, 4, new byte[16]), "astc.ktx2");
        astc.Should().Throw<InvalidDataException>().WithMessage("*Vulkan format 157*BC1, BC5 or BC7*");
        var png = () => Ktx2.Read(new byte[100], "picture.png");
        png.Should().Throw<InvalidDataException>().WithMessage("*not a KTX2 file*");
    }
}
