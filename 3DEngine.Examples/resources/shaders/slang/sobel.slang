// raylib's sobel.fs, written in Slang: the picture's edges, by Sobel's kernels.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

// raylib's uniform, which keeps the value it starts with since the example does not set it
static const float2 resolution = float2(800, 450);

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float x = 1.0/resolution.x;
    float y = 1.0/resolution.y;

    float4 horizEdge = float4(0.0);
    horizEdge -= texture0(float2(fragTexCoord.x - x, fragTexCoord.y - y))*1.0;
    horizEdge -= texture0(float2(fragTexCoord.x - x, fragTexCoord.y    ))*2.0;
    horizEdge -= texture0(float2(fragTexCoord.x - x, fragTexCoord.y + y))*1.0;
    horizEdge += texture0(float2(fragTexCoord.x + x, fragTexCoord.y - y))*1.0;
    horizEdge += texture0(float2(fragTexCoord.x + x, fragTexCoord.y    ))*2.0;
    horizEdge += texture0(float2(fragTexCoord.x + x, fragTexCoord.y + y))*1.0;

    float4 vertEdge = float4(0.0);
    vertEdge -= texture0(float2(fragTexCoord.x - x, fragTexCoord.y - y))*1.0;
    vertEdge -= texture0(float2(fragTexCoord.x    , fragTexCoord.y - y))*2.0;
    vertEdge -= texture0(float2(fragTexCoord.x + x, fragTexCoord.y - y))*1.0;
    vertEdge += texture0(float2(fragTexCoord.x - x, fragTexCoord.y + y))*1.0;
    vertEdge += texture0(float2(fragTexCoord.x    , fragTexCoord.y + y))*2.0;
    vertEdge += texture0(float2(fragTexCoord.x + x, fragTexCoord.y + y))*1.0;

    float3 edge = sqrt((horizEdge.rgb*horizEdge.rgb) + (vertEdge.rgb*vertEdge.rgb));

    return float4(edge, texture0(fragTexCoord).a);
}
