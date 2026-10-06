// raylib's blur.fs, written in Slang: the picture blurred along a diagonal by a Gaussian.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// The size of what is drawn, which raylib's shader is given as constants
static const float renderWidth = 800;
static const float renderHeight = 450;

static const float offset[3] = { 0.0, 1.3846153846, 3.2307692308 };
static const float weight[3] = { 0.2270270270, 0.3162162162, 0.0702702703 };

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float3 texelColor = texture0(fragTexCoord).rgb*weight[0];

    for (int i = 1; i < 3; i++)
    {
        texelColor += texture0(fragTexCoord + float2(offset[i])/renderWidth).rgb*weight[i];
        texelColor += texture0(fragTexCoord - float2(offset[i])/renderWidth).rgb*weight[i];
    }

    return float4(texelColor, 1.0);
}
