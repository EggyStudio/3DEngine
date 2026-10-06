// raylib's depth_write.fs, written in Slang: the color drawn, and a depth of one less its blue,
// which puts the bluer shapes in front. raylib's has written the blue since its shader took an
// output of its own (54ccb18e, 2023), where it wrote one less the pixel's own depth before, which
// the screenshot beside the example still shows.
import engine;

struct DepthWriteOutput
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
DepthWriteOutput fragmentMain(VertexOutput input)
{
    float4 texelColor = boundTexture.Sample(input.uv);

    DepthWriteOutput output;
    output.color = texelColor*input.color;
    output.depth = 1.0 - output.color.z;
    return output;
}
