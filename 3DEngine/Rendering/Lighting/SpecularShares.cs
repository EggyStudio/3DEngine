using System.Numerics;

namespace Engine;

/// <summary>
/// The share of the light from all around that a surface reflects by its specular lobe, as the
/// split sum takes it apart from the light, the scale on its F0 and the bias added, by the cosine
/// between its normal and the eye across and its roughness down, which the model pass reads for
/// every reflection of the indirect light (<c>ambientReflected</c> in <c>lights.slang</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each texel integrates GGX's lobe at its middle's cosine and roughness by importance sampling, as
/// Karis gives it for the split sum, the half ways drawn by GGX's distribution over a Hammersley
/// set and each weighed by Smith's height-correlated shadowing for the two ways taken together,
/// times the cosine between the eye and the half way over that of the normal and the half way,
/// Schlick's Fresnel apart into the scale and the bias.
/// </para>
/// <para>
/// A table rather than Karis's fit for mobile, which gives a surface seen square on less of the
/// light around it than GGX's lobe gives at a roughness of 0.6, and more at a roughness of 1
/// (<c>RoughReflectionTests</c>).
/// </para>
/// </remarks>
internal static class SpecularShares
{
    /// <summary>The table's texels along each side.</summary>
    public const int Size = 32;

    // The half ways drawn for each texel.
    private const int Samples = 1024;

    /// <summary>
    /// The table as half floats, four a texel, the scale in the first and the bias in the second,
    /// row by row from the smoothest, each row from the cosine nearest 0, each texel at its middle.
    /// </summary>
    public static Half[] Table()
    {
        var table = new Half[Size * Size * 4];
        Parallel.For(0, Size, row =>
        {
            var roughness = (row + 0.5f) / Size;
            for (int column = 0; column < Size; column++)
            {
                var (scale, bias) = At((column + 0.5f) / Size, roughness);
                var at = (row * Size + column) * 4;
                (table[at], table[at + 1], table[at + 2], table[at + 3]) = ((Half)scale, (Half)bias, Half.Zero, Half.One);
            }
        });
        return table;
    }

    /// <summary>The scale on F0 and the bias of a lobe of <paramref name="roughness"/> seen at a cosine of <paramref name="nv"/> to its normal.</summary>
    public static (float Scale, float Bias) At(float nv, float roughness)
    {
        var alpha = roughness * roughness;
        var a2 = alpha * alpha;
        var v = new Vector3(MathF.Sqrt(MathF.Max(0, 1 - nv * nv)), 0, nv);
        float scale = 0, bias = 0;
        for (int i = 0; i < Samples; i++)
        {
            // Hammersley's point, the index over the count and its bits reversed.
            var (u1, u2) = ((i + 0.5f) / Samples, Reversed((uint)i) / 4294967296f);
            var phi = 2 * MathF.PI * u1;
            var cosTheta = MathF.Sqrt((1 - u2) / (1 + (a2 - 1) * u2));
            var sinTheta = MathF.Sqrt(MathF.Max(0, 1 - cosTheta * cosTheta));
            var h = new Vector3(sinTheta * MathF.Cos(phi), sinTheta * MathF.Sin(phi), cosTheta);
            var vh = Vector3.Dot(v, h);
            var l = 2 * vh * h - v;
            var nl = l.Z;
            if (nl <= 0 || vh <= 0) continue;
            var nh = h.Z;
            // Smith's height-correlated visibility, G over 4 nl nv, as specularReflected has it.
            var visibility = 0.5f / (nl * MathF.Sqrt(nv * nv * (1 - a2) + a2) + nv * MathF.Sqrt(nl * nl * (1 - a2) + a2));
            var weight = visibility * 4 * nl * vh / nh;
            var fresnel = MathF.Pow(1 - vh, 5);
            scale += (1 - fresnel) * weight;
            bias += fresnel * weight;
        }
        return (scale / Samples, bias / Samples);
    }

    // A uint's bits in the reverse order, Van der Corput's radical inverse in base 2 times 2^32.
    private static uint Reversed(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        return ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
    }
}
