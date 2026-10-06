// raylib's cross_stitching.fs, written in Slang: the picture as stitches on a grid of six pixels.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// The size of what is drawn, which raylib's shader is given as constants
static const float renderWidth = 800.0;
static const float renderHeight = 450.0;

static const float stitchingSize = 6.0;

// raylib's uniform, which keeps the value it starts with since the example does not set it
static const int invert = 0;

// GLSL's mod, which keeps the sign of its divisor where HLSL's fmod keeps the dividend's
float glslMod(float x, float y) { return x - y*floor(x/y); }

float4 PostFX(float2 uv)
{
    float4 c = float4(0.0);
    float size = stitchingSize;
    float2 cPos = uv*float2(renderWidth, renderHeight);
    float2 tlPos = floor(cPos/float2(size, size));
    tlPos *= size;

    int remX = int(glslMod(cPos.x, size));
    int remY = int(glslMod(cPos.y, size));

    if (remX == 0 && remY == 0) tlPos = cPos;

    float2 blPos = tlPos;
    blPos.y += (size - 1.0);

    if ((remX == remY) || (((int(cPos.x) - int(blPos.x)) == (int(blPos.y) - int(cPos.y)))))
    {
        if (invert == 1) c = float4(0.2, 0.15, 0.05, 1.0);
        else c = texture0(tlPos*float2(1.0/renderWidth, 1.0/renderHeight))*1.4;
    }
    else
    {
        if (invert == 1) c = texture0(tlPos*float2(1.0/renderWidth, 1.0/renderHeight))*1.4;
        else c = float4(0.0, 0.0, 0.0, 1.0);
    }

    return c;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float3 tc = PostFX(raylibCoord(input.uv)).rgb;

    return float4(tc, 1.0);
}
