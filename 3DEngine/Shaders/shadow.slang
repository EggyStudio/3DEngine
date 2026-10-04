// The shadow pass's stages. Every surface is drawn into the shadow map with the vertex stage here
// and no fragment stage, and a masked one with the fragment stage as well, which cuts it out where
// the model pass cuts it, so its shadow has the same holes.
//
// The instances are the model pass's own, of which this stage reads the world matrix and the
// material's color and alpha mode, and the light's view-projection is a push constant, so the
// window's instances, written once a frame, are drawn through every cascade, spot tile and point
// face, each pushing its own matrix, and then by the model pass.
import modelpass;

// The rows of ModelInstance from its world matrix to its emission, which the pipeline reads from
// the same buffer at their offsets there.
public struct ShadowInstance
{
    // The world matrix as three rows of a 3x4, as ModelInstance holds it.
    public float4 worldX : INSTANCE_WORLDX;
    public float4 worldY : INSTANCE_WORLDY;
    public float4 worldZ : INSTANCE_WORLDZ;
    // The color, linear, and the alpha mode in the emission's w, as in ModelInstance.
    public float4 color : INSTANCE_COLOR;
    public float4 emission : INSTANCE_EMISSION;
};

struct ShadowPush
{
    float4x4 lightViewProjection;
};

[[vk::push_constant]]
ConstantBuffer<ShadowPush> shadowPush;

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ShadowInstance instance)
{
    float4 p = float4(position, 1.0);
    ModelVertexOutput output;
    output.world = float3(dot(instance.worldX, p), dot(instance.worldY, p), dot(instance.worldZ, p));
    output.position = mul(shadowPush.lightViewProjection, float4(output.world, 1.0));
    output.normal = float3(dot(instance.worldX.xyz, normal), dot(instance.worldY.xyz, normal), dot(instance.worldZ.xyz, normal));
    output.uv = uv;
    output.toEye = float3(0.0, 0.0, 0.0);
    output.color = instance.color;
    output.emission = instance.emission;
    output.factors = float4(0.0, 0.0, 0.0, 0.0);
    return output;
}

[shader("fragment")]
void fragmentMain(ModelVertexOutput input)
{
    // Called for the discard alone, below the material's cutoff.
    alphaTested(baseColor(input), input);
}
