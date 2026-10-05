// raylib's depth_render.fs, written in Slang: a target's depth, near to far as black to white,
// brought from the projection's curve toward a straight line.
import engine;

Sampler2D depthTexture;
uniform int flipY;

static const float nearPlane = 0.1;
static const float farPlane = 100.0;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // Handle potential Y-flipping
    float2 texCoord = input.uv;
    if (flipY != 0) texCoord.y = 1.0 - texCoord.y;

    // Sample depth
    float depth = depthTexture.Sample(texCoord).r;

    // Linearize depth value
    float linearDepth = (2.0*nearPlane)/(farPlane + nearPlane - depth*(farPlane - nearPlane));

    // Output final color
    return float4(float3(linearDepth), 1.0);
}
