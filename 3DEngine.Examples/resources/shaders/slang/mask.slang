// raylib's mask.fs, written in Slang for a model: the texture where a moving mask is bright, and
// nothing where it is dark, unlit, as raylib's default model shader draws.
import modelpass;

Sampler2D mask;

uniform int frame;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 maskColour = mask.Sample(input.uv + float2(sin(-frame / 150.0) / 10.0, cos(-frame / 170.0) / 10.0));
    if (maskColour.r < 0.25) discard;

    // The material's texture is decoded to linear light as the pass samples it, and is encoded
    // again so it multiplies the mask as raylib's bytes do.
    float4 texelColor = boundTexture.Sample(input.uv + float2(sin(frame / 90.0) / 8.0, cos(frame / 60.0) / 8.0));
    return float4(linearToSrgb(texelColor.rgb), texelColor.a) * maskColour;
}
