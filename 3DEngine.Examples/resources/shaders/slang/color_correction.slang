// raylib's color_correction.fs, written in Slang: contrast, brightness and saturation, each from
// -100 to 100.
import engine;

uniform float contrast;
uniform float saturation;
uniform float brightness;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 texel = boundTexture.Sample(input.uv);

    texel.rgb = (texel.rgb - 0.5) * (contrast / 100.0 + 1.0) + 0.5;

    texel.rgb = texel.rgb + brightness / 100.0;

    float intensity = dot(texel.rgb, float3(0.299, 0.587, 0.114));
    texel.rgb = (texel.rgb - intensity) * saturation / 100.0 + texel.rgb;

    return texel;
}
