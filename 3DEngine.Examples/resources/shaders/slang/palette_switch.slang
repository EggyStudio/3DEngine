// raylib's palette_switch.fs, written in Slang: each pixel's red, as an index, takes its color from
// the palette.
import engine;

static const int MAX_INDEXED_COLORS = 8;

uniform int3 palette[MAX_INDEXED_COLORS];

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // The index the program drew as the color's red
    float4 texelColor = boundTexture.Sample(input.uv) * input.color;
    int index = int(texelColor.r * 255.0);
    int3 color = palette[index];

    return float4(float3(color) / 255.0, texelColor.a);
}
