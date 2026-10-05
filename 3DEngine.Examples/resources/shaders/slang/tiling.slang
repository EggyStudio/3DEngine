// raylib's tiling.fs, written in Slang for a model: the material's texture repeated across each
// face, unlit, as raylib's default model shader draws.
import modelpass;

uniform float2 tiling;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float2 texCoord = input.uv * tiling;

    // Linear inside the pass, so encoded again for the frame without the lit pass's tonemap
    float4 color = materialColor(input) * boundTexture.Sample(texCoord);
    return float4(linearToSrgb(color.rgb), color.a);
}
