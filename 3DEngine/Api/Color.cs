using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>A color of four bytes, red, green, blue and alpha, in that order in memory.</summary>
/// <remarks>
/// The byte order matches the vertex format the immediate pass reads (<c>R8G8B8A8_UNORM</c>), so a
/// color is written into a vertex as it is. The named colors are raylib's, so a program ported
/// from raylib keeps its palette.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>The color as four floats from 0 to 1.</summary>
    public Vector4 ToVector4() => new(R / 255f, G / 255f, B / 255f, A / 255f);

    /// <summary>The same color with a different alpha, from 0 to 1.</summary>
    public Color Fade(float alpha) => this with { A = (byte)(Math.Clamp(alpha, 0f, 1f) * 255f) };

    /// <summary>The color packed as ImGui packs it, alpha in the high byte.</summary>
    internal uint ToImGui() => (uint)(R | (G << 8) | (B << 16) | (A << 24));

    /// <summary>Light gray (200, 200, 200).</summary>
    public static readonly Color LightGray = new(200, 200, 200);
    /// <summary>Gray (130, 130, 130).</summary>
    public static readonly Color Gray = new(130, 130, 130);
    /// <summary>Dark gray (80, 80, 80).</summary>
    public static readonly Color DarkGray = new(80, 80, 80);
    /// <summary>Yellow (253, 249, 0).</summary>
    public static readonly Color Yellow = new(253, 249, 0);
    /// <summary>Gold (255, 203, 0).</summary>
    public static readonly Color Gold = new(255, 203, 0);
    /// <summary>Orange (255, 161, 0).</summary>
    public static readonly Color Orange = new(255, 161, 0);
    /// <summary>Pink (255, 109, 194).</summary>
    public static readonly Color Pink = new(255, 109, 194);
    /// <summary>Red (230, 41, 55).</summary>
    public static readonly Color Red = new(230, 41, 55);
    /// <summary>Maroon (190, 33, 55).</summary>
    public static readonly Color Maroon = new(190, 33, 55);
    /// <summary>Green (0, 228, 48).</summary>
    public static readonly Color Green = new(0, 228, 48);
    /// <summary>Lime (0, 158, 47).</summary>
    public static readonly Color Lime = new(0, 158, 47);
    /// <summary>Dark green (0, 117, 44).</summary>
    public static readonly Color DarkGreen = new(0, 117, 44);
    /// <summary>Sky blue (102, 191, 255).</summary>
    public static readonly Color SkyBlue = new(102, 191, 255);
    /// <summary>Blue (0, 121, 241).</summary>
    public static readonly Color Blue = new(0, 121, 241);
    /// <summary>Dark blue (0, 82, 172).</summary>
    public static readonly Color DarkBlue = new(0, 82, 172);
    /// <summary>Purple (200, 122, 255).</summary>
    public static readonly Color Purple = new(200, 122, 255);
    /// <summary>Violet (135, 60, 190).</summary>
    public static readonly Color Violet = new(135, 60, 190);
    /// <summary>Dark purple (112, 31, 126).</summary>
    public static readonly Color DarkPurple = new(112, 31, 126);
    /// <summary>Beige (211, 176, 131).</summary>
    public static readonly Color Beige = new(211, 176, 131);
    /// <summary>Brown (127, 106, 79).</summary>
    public static readonly Color Brown = new(127, 106, 79);
    /// <summary>Dark brown (76, 63, 47).</summary>
    public static readonly Color DarkBrown = new(76, 63, 47);
    /// <summary>White (255, 255, 255).</summary>
    public static readonly Color White = new(255, 255, 255);
    /// <summary>Black (0, 0, 0).</summary>
    public static readonly Color Black = new(0, 0, 0);
    /// <summary>Transparent black (0, 0, 0, 0).</summary>
    public static readonly Color Blank = new(0, 0, 0, 0);
    /// <summary>Magenta (255, 0, 255).</summary>
    public static readonly Color Magenta = new(255, 0, 255);
    /// <summary>The off-white raylib clears to (245, 245, 245).</summary>
    public static readonly Color RayWhite = new(245, 245, 245);
}
