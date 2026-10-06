// raylib's predator.fs, written in Slang: the picture's brightness as a heat map, blue to yellow
// to red.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float3 texelColor = texture0(raylibCoord(input.uv)).rgb;

    float3 colors[3];
    colors[0] = float3(0.0, 0.0, 1.0);
    colors[1] = float3(1.0, 1.0, 0.0);
    colors[2] = float3(1.0, 0.0, 0.0);

    float lum = (texelColor.r + texelColor.g + texelColor.b)/3.0;

    int ix = (lum < 0.5)? 0:1;

    float3 tc = lerp(colors[ix], colors[ix + 1], (lum - float(ix)*0.5)/0.5);

    return float4(tc, 1.0);
}
