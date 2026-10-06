// raylib's bloom.fs, written in Slang: the picture with a blur of itself added, so its light
// spreads.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

static const float2 size = float2(800, 450);   // Framebuffer size
static const float samples = 5.0;          // Pixels per axis; higher = bigger glow, worse performance
static const float quality = 2.5;          // Defines size factor: Lower = smaller glow, better quality

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float4 sum = float4(0);
    float2 sizeFactor = float2(1)/size*quality;

    float4 source = texture0(fragTexCoord);

    const int range = 2;            // should be = (samples - 1)/2;

    for (int x = -range; x <= range; x++)
    {
        for (int y = -range; y <= range; y++)
        {
            sum += texture0(fragTexCoord + float2(x, y)*sizeFactor);
        }
    }

    // The color drawn with stands for raylib's colDiffuse, white in 2D
    return ((sum/(samples*samples)) + source)*input.color;
}
