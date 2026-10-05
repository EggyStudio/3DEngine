// raylib's color_mix.fs, written in Slang: two textures mixed across a soft line at the divider.
import engine;

Sampler2D texture1;

uniform float divider;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 texelColor0 = boundTexture.Sample(input.uv);
    float4 texelColor1 = texture1.Sample(input.uv);

    float x = frac(input.uv.x);
    float final = smoothstep(divider - 0.1, divider + 0.1, x);

    return lerp(texelColor0, texelColor1, final);
}
