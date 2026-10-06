// raylib's pixelizer.fs, written in Slang: the picture in blocks of five pixels.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// The size of what is drawn, which raylib's shader is given as constants
static const float renderWidth = 800;
static const float renderHeight = 450;

// raylib's uniforms, which keep the values they start with since the example sets neither
static const float pixelWidth = 5.0;
static const float pixelHeight = 5.0;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float dx = pixelWidth*(1.0/renderWidth);
    float dy = pixelHeight*(1.0/renderHeight);

    float2 coord = float2(dx*floor(fragTexCoord.x/dx), dy*floor(fragTexCoord.y/dy));

    float3 tc = texture0(coord).rgb;

    return float4(tc, 1.0);
}
