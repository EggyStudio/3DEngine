using System.Numerics;

namespace Engine;

/// <summary>
/// A head-mounted display's measures, which <see cref="Engine3D.LoadVrStereoConfig"/> works a
/// stereo frame out from, raylib's <c>VrDeviceInfo</c>.
/// </summary>
public struct VrDeviceInfo
{
    /// <summary>The display's width in pixels, both eyes together.</summary>
    public int HResolution;

    /// <summary>The display's height in pixels.</summary>
    public int VResolution;

    /// <summary>The display's width in meters.</summary>
    public float HScreenSize;

    /// <summary>The display's height in meters.</summary>
    public float VScreenSize;

    /// <summary>How far the eyes are from the display, in meters.</summary>
    public float EyeToScreenDistance;

    /// <summary>How far apart the lenses' centers are, in meters.</summary>
    public float LensSeparationDistance;

    /// <summary>How far apart the pupils are, in meters.</summary>
    public float InterpupillaryDistance;

    /// <summary>The lenses' distortion, four constants of a polynomial in the square of the radius.</summary>
    public Vector4 LensDistortionValues;

    /// <summary>The lenses' chromatic aberration, scales of red and blue against green, in four constants.</summary>
    public Vector4 ChromaAbCorrection;
}
