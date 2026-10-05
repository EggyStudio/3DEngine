// raylib's swirl.fs, written in Slang: the texture turned in a swirl about a center.
import engine;

// The size the texture is drawn at, which raylib's shader takes as given
static const float renderWidth = 800;
static const float renderHeight = 450;

static const float radius = 250.0;
static const float angle = 0.8;

// In pixels counted from the bottom, as raylib's program gives it
uniform float2 center;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 texSize = float2(renderWidth, renderHeight);

    // raylib draws its target turned, so its texture coordinates rise up the screen as the center
    // does. Here the target is drawn as it is, so they are turned in and turned back out.
    float2 tc = float2(input.uv.x, 1.0 - input.uv.y) * texSize;
    tc -= center;

    float dist = length(tc);
    if (dist < radius)
    {
        float percent = (radius - dist) / radius;
        float theta = percent * percent * angle * 8.0;
        float s = sin(theta);
        float c = cos(theta);
        tc = float2(dot(tc, float2(c, -s)), dot(tc, float2(s, c)));
    }

    tc += center;

    float2 uv = tc / texSize;
    float4 color = boundTexture.Sample(float2(uv.x, 1.0 - uv.y)) * input.color;
    return float4(color.rgb, 1.0);
}
