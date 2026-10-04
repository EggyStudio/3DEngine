namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Spreads light brighter than <paramref name="threshold"/> into the pixels around it, as a
    /// bright light glows through a lens, adding <paramref name="intensity"/> of it to the frame, or
    /// turns bloom off with an intensity of 0, which it is by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With bloom on, the window's models, and the shapes drawn up to the last one inside
    /// <see cref="BeginMode3D"/>, are drawn into a frame that holds light past 1, which bloom
    /// spreads and a last pass tonemaps into the window. Shapes, text and ImGui drawn after that go
    /// on top unchanged, so a game's interface keeps its exact colors.
    /// </para>
    /// <para>
    /// Light past 1 comes from emissive materials and strong lights, so a threshold of 1 blooms only
    /// what is brighter than white. An intensity of 1 adds as much light as was past the threshold,
    /// spread wide, and 0.3 to 1 suits most scenes. A shader of the program's own drawn inside
    /// <see cref="BeginMode3D"/> writes linear light while bloom is on, so its colors look lighter
    /// unless it decodes them from sRGB first.
    /// </para>
    /// </remarks>
    public static void SetBloom(float intensity, float threshold = 1)
    {
        var bloom = World.GetOrInsertResource(() => new BloomSettings());
        bloom.Intensity = Math.Max(0, intensity);
        bloom.Threshold = Math.Max(0, threshold);
    }
}
