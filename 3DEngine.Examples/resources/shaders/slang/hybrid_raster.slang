// raylib's hybrid_raster.fs, written in Slang: the color drawn, with its blue written as its depth,
// since a shader that writes depth for some of a pass is written for all. raylib's has written the
// blue since its review of the example (6820ff61, 2025), where it wrote the pixel's own depth
// before, which the screenshot beside the example still shows.
import engine;

struct HybridRasterOutput
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
HybridRasterOutput fragmentMain(VertexOutput input)
{
    float4 texelColor = boundTexture.Sample(input.uv);

    HybridRasterOutput output;
    output.color = texelColor*input.color;
    output.depth = output.color.z;
    return output;
}
