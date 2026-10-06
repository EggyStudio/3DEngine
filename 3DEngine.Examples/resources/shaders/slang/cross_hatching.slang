// raylib's cross_hatching.fs, written in Slang: lines across the picture, more of them where it is
// darker.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// The height of what is drawn, since OpenGL counts gl_FragCoord's rows up from the bottom and
// Vulkan down from the top
static const float renderHeight = 450;

static const float hatchOffsetY = 5.0;
static const float lumThreshold01 = 0.9;
static const float lumThreshold02 = 0.7;
static const float lumThreshold03 = 0.5;
static const float lumThreshold04 = 0.3;

// GLSL's mod, which keeps the sign of its divisor where HLSL's fmod keeps the dividend's
float glslMod(float x, float y) { return x - y*floor(x/y); }

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragCoord = float2(input.position.x, renderHeight - input.position.y);
    float3 tc = float3(1.0, 1.0, 1.0);
    float lum = length(texture0(raylibCoord(input.uv)).rgb);

    if (lum < lumThreshold01)
    {
        if (glslMod(fragCoord.x + fragCoord.y, 10.0) == 0.0) tc = float3(0.0, 0.0, 0.0);
    }

    if (lum < lumThreshold02)
    {
        if (glslMod(fragCoord.x - fragCoord.y, 10.0) == 0.0) tc = float3(0.0, 0.0, 0.0);
    }

    if (lum < lumThreshold03)
    {
        if (glslMod(fragCoord.x + fragCoord.y - hatchOffsetY, 10.0) == 0.0) tc = float3(0.0, 0.0, 0.0);
    }

    if (lum < lumThreshold04)
    {
        if (glslMod(fragCoord.x - fragCoord.y - hatchOffsetY, 10.0) == 0.0) tc = float3(0.0, 0.0, 0.0);
    }

    return float4(tc, 1.0);
}
