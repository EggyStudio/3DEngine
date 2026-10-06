// raylib's posterization.fs, written in Slang: the picture cut down to eight shades a channel.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

static const float gamma = 0.6;
static const float numColors = 8.0;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float3 texelColor = texture0(raylibCoord(input.uv)).rgb;

    texelColor = pow(texelColor, float3(gamma, gamma, gamma));
    texelColor = texelColor*numColors;
    texelColor = floor(texelColor);
    texelColor = texelColor/numColors;
    texelColor = pow(texelColor, float3(1.0/gamma));

    return float4(texelColor, 1.0);
}
