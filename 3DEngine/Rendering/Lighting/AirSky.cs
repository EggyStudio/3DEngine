using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The air's sky as a pass draws it, the Sky module's air, the way toward the sun and the sun's
/// light outside the air times what a luminance is multiplied by for the frame, which an
/// environment map made from it holds in place of an image (<see cref="EnvironmentMap.FromAir"/>).
/// </summary>
internal sealed record AirSky(Sky.Atmosphere Air, Vector3 SunToward, Vector3 Light)
{
    /// <summary>
    /// The tables' sizes, the cosine with the zenith across and the height down, as the Sky
    /// module's SkyLight makes its own and atmosphere.slang reads them.
    /// </summary>
    internal const uint TransmittanceAngles = 256, TransmittanceHeights = 32, ScatterAngles = 32, ScatterHeights = 32;

    // As atmosphere.slang's AirPush lays the numbers out, eight float4.
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Vector4 Rayleigh, Aerosol, Ozone, Ground, Planet, Sun, Light;
        public uint Width, Height, Unused0, Unused1;
    }

    /// <summary>The pass's push constants for an image of <paramref name="width"/> by <paramref name="height"/> texels.</summary>
    public byte[] PushFor(uint width, uint height)
    {
        var air = Air;
        var push = new Push
        {
            Rayleigh = new Vector4(air.RayleighScattering, air.RayleighScaleHeight),
            Aerosol = new Vector4(air.AerosolAlbedo * air.AerosolExtinction, air.AerosolScaleHeight),
            Ozone = new Vector4(air.OzoneAbsorption, air.OzoneBottom),
            Ground = new Vector4(air.GroundAlbedo, air.OzoneWidth),
            Planet = new Vector4(air.PlanetRadius, air.Top, air.AerosolExtinction, air.AerosolAnisotropy),
            Sun = new Vector4(Vector3.Normalize(SunToward), 0),
            Light = new Vector4(Light, 0),
            Width = width,
            Height = height,
        };
        return MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)).ToArray();
    }

    /// <summary>The size of <see cref="PushFor"/>'s bytes, which the pass's pipelines take.</summary>
    public static uint PushSize => (uint)Marshal.SizeOf<Push>();
}
