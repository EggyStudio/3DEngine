using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The air's sky as a pass draws it, the Sky module's air, the way toward the sun and the sun's
/// light outside the air times what a luminance is multiplied by for the frame, or the moon's in
/// their place by night, and the luminance of a moonless sky's own glow under it in the same
/// light, which an environment map made from it holds in place of an image
/// (<see cref="EnvironmentMap.FromAir"/>).
/// </summary>
internal sealed record AirSky(Sky.Atmosphere Air, Vector3 SunToward, Vector3 Light, Vector3 Night = default)
{
    // As atmosphere.slang's AirPush lays the numbers out, four float4.
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Vector4 Sun, Scale, Night;
        public uint Width, Height, Unused0, Unused1;
    }

    /// <summary>The pass's push constants for an image of <paramref name="width"/> by <paramref
    /// name="height"/> texels.</summary>
    /// <remarks>
    /// The tables give the sky in the module's own sun outside the air, so the frame's light is
    /// given as its share of that sun's, a color's channel at a time.
    /// </remarks>
    public byte[] PushFor(uint width, uint height)
    {
        var push = new Push
        {
            Sun = new Vector4(Vector3.Normalize(SunToward), 0),
            Scale = new Vector4(Light / Sky.Sun.Outside, 0),
            Night = new Vector4(Night, 0),
            Width = width,
            Height = height,
        };
        return MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)).ToArray();
    }

    /// <summary>The size of <see cref="PushFor"/>'s bytes, which the pass's
    /// pipelines take.</summary>
    public static uint PushSize => (uint)Marshal.SizeOf<Push>();
}
