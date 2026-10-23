namespace Engine;

/// <summary>
/// The pixel art the day sky draws the sun and the moon with, each a square body with its glow
/// around it, as RGBA bytes a row at a time from the top, a clear pixel's alpha 0, the voxel game's.
/// </summary>
/// <remarks>
/// A body is 8 pixels across at the middle of a picture 32 across, as Minecraft's sun and moon are,
/// with a faint glow falling from the body's edge to nothing before the picture's edge. Each pixel is
/// painted as <see cref="Fine"/> texels a side, so the body keeps its square pixels and the glow
/// falls smoothly. The pictures are added to the sky, each texel's color by its alpha, so the glow
/// brightens the sky behind it. The moon's phases are one sheet four across and two down, phase 0
/// the full moon at the top left and 4 the new moon at the bottom left, waning through the first
/// row and waxing through the second, each with a clear texel around it, since a sample at the edge
/// of a phase, taken without filtering, may land on the texel past it.
/// </remarks>
internal static class SkyArt
{
    /// <summary>How many pixels the sun's and the moon's bodies are across.</summary>
    public const int Size = 8;

    /// <summary>How many pixels the sun's and the moon's pictures are across, the body at the middle and its glow around it.</summary>
    public const int Across = 32;

    /// <summary>How many texels the pictures are painted at to each of their pixels.</summary>
    public const int Fine = 4;

    /// <summary>How many texels the pictures are across.</summary>
    public const int Texels = Across * Fine;

    /// <summary>How many phases the moon goes through, a day each.</summary>
    public const int Phases = 8;

    /// <summary>How many texels apart the moon's phases are in their sheet, each with its clear edge.</summary>
    public const int Cell = Texels + 2;

    /// <summary>The brightest the glow is, beside the body, as a share of its color.</summary>
    public const float GlowPeak = 0.1f;

    /// <summary>How far from the picture's middle the glow is gone, in pixels, short of the picture's edge.</summary>
    public const float GlowReach = 15;

    // The sun's rings from its edge in, the core the palest.
    private static readonly string[] Sun =
    [
        "oooooooo",
        "ommmmmmo",
        "omccccmo",
        "omccccmo",
        "omccccmo",
        "omccccmo",
        "ommmmmmo",
        "oooooooo",
    ];

    // The moon's face and its darker craters.
    private static readonly string[] Moon =
    [
        "wwwwwwww",
        "wwwwwgww",
        "wggwwwww",
        "wggwwwgw",
        "wwwwwwww",
        "wwwwggww",
        "wgwwggww",
        "wwwwwwww",
    ];

    // How far round the light on the moon has turned in each phase, in half turns, the crescents a
    // little short of the eighth past the quarter, so the pixels they keep run unbroken from tip to
    // tip rather than one breaking off.
    private static readonly float[] Turns = [0, 0.25f, 0.5f, 0.7f, 1, 1.3f, 1.5f, 1.75f];

    private static (byte R, byte G, byte B) Shade(char pixel) => pixel switch
    {
        'o' => (255, 196, 80),
        'm' => (255, 230, 130),
        'c' => (255, 252, 222),
        'w' => (226, 230, 240),
        'g' => (176, 182, 198),
        _ => throw new ArgumentOutOfRangeException(nameof(pixel)),
    };

    // The colors the sun's and the moon's glows are of, a warm yellow and the moonlight's blue.
    private static readonly (byte R, byte G, byte B) SunGlow = (255, 226, 120), MoonGlow = (150, 170, 230);

    /// <summary>The sun, <see cref="Texels"/> texels square, its body at the middle and its glow around it.</summary>
    public static byte[] SunPixels()
    {
        var pixels = new byte[Texels * Texels * 4];
        PaintGlow(pixels, Texels, 0, 0, SunGlow, 1);
        PaintBody(pixels, Texels, (Texels - Size * Fine) / 2, (Texels - Size * Fine) / 2, Sun, (_, _) => true);
        return pixels;
    }

    /// <summary>How bright the glow is at a texel of a picture, from nothing to <see cref="GlowPeak"/>, falling evenly from the body's edge to nothing at <see cref="GlowReach"/> pixels from the middle.</summary>
    public static float Glow(int x, int y)
    {
        float across = (x + 0.5f) / Fine - Across / 2f, down = (y + 0.5f) / Fine - Across / 2f;
        return GlowPeak * Math.Clamp((GlowReach - MathF.Sqrt(across * across + down * down)) / (GlowReach - Size / 2f), 0, 1);
    }

    /// <summary>The sheet of the moon's phases, <see cref="Cell"/> times four texels across and twice <see cref="Cell"/> down, each phase's dark side clear and its glow as bright as it is lit.</summary>
    public static byte[] MoonPixels()
    {
        const int across = Cell * 4;
        var sheet = new byte[across * Cell * 2 * 4];
        for (int phase = 0; phase < Phases; phase++)
        {
            int x0 = phase % 4 * Cell + 1, y0 = phase / 4 * Cell + 1, lit = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (Lit(phase, x, y)) lit++;
            PaintGlow(sheet, across, x0, y0, MoonGlow, lit / (float)(Size * Size));
            PaintBody(sheet, across, x0 + (Texels - Size * Fine) / 2, y0 + (Texels - Size * Fine) / 2, Moon, (x, y) => Lit(phase, x, y));
        }
        return sheet;
    }

    /// <summary>
    /// Whether a pixel of the moon is lit in a phase, the moon taken as a ball lit from a way that
    /// turns from behind the eye at the full moon to behind the moon at the new one, from the left
    /// while it wanes and from the right while it waxes.
    /// </summary>
    public static bool Lit(int phase, int x, int y)
    {
        float u = (x + 0.5f) / Size * 2 - 1, v = (y + 0.5f) / Size * 2 - 1;
        // The square's pixel is taken to the place on the round face it stands for, drawn in along
        // its way from the middle until its distance is the larger of its two offsets, so the
        // square's edge is the ball's edge, a crescent thins toward its tips and the full moon's
        // corners are lit.
        var round = MathF.Sqrt(u * u + v * v);
        if (round > 0) (u, v) = (u * MathF.Max(MathF.Abs(u), MathF.Abs(v)) / round, v * MathF.Max(MathF.Abs(u), MathF.Abs(v)) / round);
        var depth = MathF.Sqrt(MathF.Max(0, 1 - u * u - v * v));
        var angle = Turns[phase] * MathF.PI;
        return -u * MathF.Sin(angle) + depth * MathF.Cos(angle) > 1e-4f;
    }

    // A glow of a color over a picture's square of Texels at a place in a sheet so many texels wide,
    // as bright as a share of its own.
    private static void PaintGlow(byte[] pixels, int width, int x0, int y0, (byte R, byte G, byte B) color, float share)
    {
        for (int y = 0; y < Texels; y++)
            for (int x = 0; x < Texels; x++)
            {
                var at = ((y0 + y) * width + x0 + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2]) = color;
                pixels[at + 3] = (byte)MathF.Round(255 * Glow(x, y) * share);
            }
    }

    // A pattern's pixels at a place in a sheet so many texels wide, each a square of Fine texels,
    // where it shows them.
    private static void PaintBody(byte[] pixels, int width, int x0, int y0, string[] pattern, Func<int, int, bool> shown)
    {
        for (int y = 0; y < pattern.Length * Fine; y++)
            for (int x = 0; x < pattern[0].Length * Fine; x++)
            {
                if (!shown(x / Fine, y / Fine)) continue;
                var (r, g, b) = Shade(pattern[y / Fine][x / Fine]);
                var at = ((y0 + y) * width + x0 + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (r, g, b, 255);
            }
    }
}
