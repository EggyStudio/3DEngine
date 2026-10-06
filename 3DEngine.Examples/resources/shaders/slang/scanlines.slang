// raylib's scanlines.fs, written in Slang: the picture behind green lines, as an old screen's.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// The size of what is drawn, which raylib's shader is given as constants
static const float renderWidth = 800;
static const float renderHeight = 450;

static const float offset = 0.0;

uniform float time;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float frequency = renderHeight/3.0;

    // Scanlines method 2
    float globalPos = (fragTexCoord.y + offset)*frequency;
    float wavePos = cos((frac(globalPos) - 0.5)*3.14);

    float4 texelColor = texture0(fragTexCoord);

    return lerp(float4(0.0, 0.3, 0.0, 0.0), texelColor, wavePos);
}
