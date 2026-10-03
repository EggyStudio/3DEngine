namespace Engine;

/// <summary>
/// Generates a complete mip chain for a base-level <see cref="Texture"/> using a 2x2 box
/// filter. Cheap, deterministic, and good enough for the runtime - higher quality
/// (Kaiser, separable Lanczos) can be slotted in later behind the same entry point.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope:</b> only the uncompressed formats the engine actually decodes today are
/// supported (<see cref="TextureFormat.Rgba8"/>, <see cref="TextureFormat.Rgba16F"/>,
/// <see cref="TextureFormat.Rgba32F"/>). Block-compressed inputs throw
/// <see cref="NotSupportedException"/> - those should arrive pre-mipped from KTX2 / DDS.
/// </para>
/// <para>
/// <b>Mip count:</b> the chain runs down to a 1x1 base level
/// (<c>floor(log2(max(W,H))) + 1</c>). Each level rounds extents down with a floor of 1
/// to match the GPU sampling rules.
/// </para>
/// <para>
/// <b>Color space:</b> filtering is performed on the stored values verbatim - if the
/// texture is sRGB-encoded the box filter happens in non-linear space, which is
/// "correct enough" for diffuse textures and matches what most engines ship by default.
/// HDR floats filter naturally in linear space. A future linear-aware path can branch
/// on <see cref="Texture.ColorSpace"/> if banding becomes visible.
/// </para>
/// </remarks>
/// <seealso cref="Texture"/>
public static class TextureMipGenerator
{
    private static readonly ILogger Logger = Log.Category("Engine.Textures");

    /// <summary>
    /// Returns a copy of <paramref name="source"/> with a full box-filtered mip chain
    /// appended. When <paramref name="source"/> already has more than one level, it is
    /// returned unchanged.
    /// </summary>
    public static Texture WithMipChain(Texture source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.MipCount > 1) return source;
        if (TextureFormatInfo.IsBlockCompressed(source.Format))
            throw new NotSupportedException(
                $"TextureMipGenerator: cannot mip block-compressed format {source.Format}; supply pre-mipped data.");

        int w = source.Width;
        int h = source.Height;
        if (w <= 1 && h <= 1) return source;

        int bpp = TextureFormatInfo.BytesPerPixel(source.Format);
        int mipCount = MipCount(w, h);
        int totalBytes = TotalBytes(w, h, bpp, mipCount);

        var dst = new byte[totalBytes];
        // Level 0: copy verbatim.
        int baseBytes = w * h * bpp;
        Buffer.BlockCopy(source.Pixels, 0, dst, 0, baseBytes);

        int srcOffset = 0;
        int dstOffset = baseBytes;
        int prevW = w, prevH = h;
        for (int level = 1; level < mipCount; level++)
        {
            int curW = Math.Max(1, prevW >> 1);
            int curH = Math.Max(1, prevH >> 1);
            DownsampleBox(source.Format, dst, srcOffset, prevW, prevH, dst, dstOffset, curW, curH);
            srcOffset = dstOffset;
            dstOffset += curW * curH * bpp;
            prevW = curW;
            prevH = curH;
        }

        Logger.Debug(
            $"TextureMipGenerator: '{source.SourcePath}' {w}x{h} {source.Format} - " +
            $"generated {mipCount} mip(s), {totalBytes} bytes total.");

        return new Texture
        {
            Pixels = dst,
            Width = w,
            Height = h,
            MipCount = mipCount,
            Format = source.Format,
            ColorSpace = source.ColorSpace,
            SourcePath = source.SourcePath,
            SourceFormat = source.SourceFormat,
        };
    }

    /// <summary>Number of mip levels in a complete chain for <paramref name="w"/>x<paramref name="h"/>.</summary>
    public static int MipCount(int w, int h)
    {
        int max = Math.Max(w, h);
        int count = 1;
        while (max > 1) { max >>= 1; count++; }
        return count;
    }

    /// <summary>Total byte size of a packed mip chain (base + every reduction down to 1x1).</summary>
    public static int TotalBytes(int w, int h, int bytesPerPixel, int mipCount)
    {
        int total = 0;
        for (int i = 0; i < mipCount; i++)
        {
            int lw = Math.Max(1, w >> i);
            int lh = Math.Max(1, h >> i);
            total += lw * lh * bytesPerPixel;
        }
        return total;
    }

    private static void DownsampleBox(
        TextureFormat format,
        byte[] src, int srcOffset, int srcW, int srcH,
        byte[] dst, int dstOffset, int dstW, int dstH)
    {
        switch (format)
        {
            case TextureFormat.Rgba8:   DownsampleRgba8(src, srcOffset, srcW, srcH, dst, dstOffset, dstW, dstH); return;
            case TextureFormat.Rgba32F: DownsampleRgba32F(src, srcOffset, srcW, srcH, dst, dstOffset, dstW, dstH); return;
            case TextureFormat.Rgba16F: DownsampleRgba16F(src, srcOffset, srcW, srcH, dst, dstOffset, dstW, dstH); return;
            default:
                throw new NotSupportedException($"TextureMipGenerator: format {format} not supported (yet).");
        }
    }

    // -- Per-format box filters: 2x2 average, with edge replication when an axis is 1 --

    private static void DownsampleRgba8(
        byte[] src, int srcOffset, int srcW, int srcH,
        byte[] dst, int dstOffset, int dstW, int dstH)
    {
        for (int y = 0; y < dstH; y++)
        {
            int sy0 = Math.Min(y * 2, srcH - 1);
            int sy1 = Math.Min(sy0 + 1, srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int sx0 = Math.Min(x * 2, srcW - 1);
                int sx1 = Math.Min(sx0 + 1, srcW - 1);

                int o00 = srcOffset + (sy0 * srcW + sx0) * 4;
                int o10 = srcOffset + (sy0 * srcW + sx1) * 4;
                int o01 = srcOffset + (sy1 * srcW + sx0) * 4;
                int o11 = srcOffset + (sy1 * srcW + sx1) * 4;
                int od  = dstOffset + (y    * dstW + x  ) * 4;
                for (int c = 0; c < 4; c++)
                {
                    int sum = src[o00 + c] + src[o10 + c] + src[o01 + c] + src[o11 + c];
                    dst[od + c] = (byte)((sum + 2) >> 2);
                }
            }
        }
    }

    private static void DownsampleRgba32F(
        byte[] src, int srcOffset, int srcW, int srcH,
        byte[] dst, int dstOffset, int dstW, int dstH)
    {
        var srcF = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(src.AsSpan(srcOffset));
        var dstF = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(dst.AsSpan(dstOffset));

        for (int y = 0; y < dstH; y++)
        {
            int sy0 = Math.Min(y * 2, srcH - 1);
            int sy1 = Math.Min(sy0 + 1, srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int sx0 = Math.Min(x * 2, srcW - 1);
                int sx1 = Math.Min(sx0 + 1, srcW - 1);

                int o00 = (sy0 * srcW + sx0) * 4;
                int o10 = (sy0 * srcW + sx1) * 4;
                int o01 = (sy1 * srcW + sx0) * 4;
                int o11 = (sy1 * srcW + sx1) * 4;
                int od  = (y    * dstW + x  ) * 4;
                for (int c = 0; c < 4; c++)
                    dstF[od + c] = 0.25f * (srcF[o00 + c] + srcF[o10 + c] + srcF[o01 + c] + srcF[o11 + c]);
            }
        }
    }

    private static void DownsampleRgba16F(
        byte[] src, int srcOffset, int srcW, int srcH,
        byte[] dst, int dstOffset, int dstW, int dstH)
    {
        var srcH16 = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(src.AsSpan(srcOffset));
        var dstH16 = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(dst.AsSpan(dstOffset));

        for (int y = 0; y < dstH; y++)
        {
            int sy0 = Math.Min(y * 2, srcH - 1);
            int sy1 = Math.Min(sy0 + 1, srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int sx0 = Math.Min(x * 2, srcW - 1);
                int sx1 = Math.Min(sx0 + 1, srcW - 1);

                int o00 = (sy0 * srcW + sx0) * 4;
                int o10 = (sy0 * srcW + sx1) * 4;
                int o01 = (sy1 * srcW + sx0) * 4;
                int o11 = (sy1 * srcW + sx1) * 4;
                int od  = (y    * dstW + x  ) * 4;
                for (int c = 0; c < 4; c++)
                {
                    float v = 0.25f * (
                        (float)srcH16[o00 + c] + (float)srcH16[o10 + c] +
                        (float)srcH16[o01 + c] + (float)srcH16[o11 + c]);
                    dstH16[od + c] = (Half)v;
                }
            }
        }
    }
}