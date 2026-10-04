// The shadow pass's stages. Every surface is drawn into the shadow map with the vertex stage here
// and no fragment stage, and a masked one with the fragment stage as well, which cuts it out where
// the model pass cuts it, so its shadow has the same holes. A blended surface that is clear in
// places is drawn with the fragment stage too, which keeps a share of its texels as large as its
// alpha, so the filtered shadow is as dark as the surface is opaque.
//
// The instances are the model pass's own, of which this stage reads the world matrix and the
// material's color and alpha mode, and the light's view-projection is modelPush, so the
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

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ShadowInstance instance)
{
    float4 p = float4(position, 1.0);
    ModelVertexOutput output;
    output.world = float3(dot(instance.worldX, p), dot(instance.worldY, p), dot(instance.worldZ, p));
    output.position = mul(modelPush.viewProjection, float4(output.world, 1.0));
    output.normal = float3(dot(instance.worldX.xyz, normal), dot(instance.worldY.xyz, normal), dot(instance.worldZ.xyz, normal));
    output.uv = uv;
    output.toEye = float3(0.0, 0.0, 0.0);
    output.color = instance.color;
    output.emission = instance.emission;
    output.factors = float4(0.0, 0.0, 0.0, 0.0);
    return output;
}

// A threshold from 0 to 1 for each texel, spread evenly over a small neighborhood (Jorge
// Jimenez's interleaved gradient noise), so the texels a blended surface keeps are scattered
// rather than banded, and the nine samples the model pass averages see its alpha.
float threshold(float2 texel)
{
    return frac(52.9829189 * frac(dot(texel, float2(0.06711056, 0.00583715))));
}

[shader("fragment")]
void fragmentMain(ModelVertexOutput input)
{
    float4 color = baseColor(input);
    // A blended surface, whose mode is 0, keeps a texel as often as it is opaque.
    if (input.emission.w == 0.0)
    {
        if (color.a <= threshold(input.position.xy))
            discard;
        return;
    }
    // Called for the discard alone, below the material's cutoff.
    alphaTested(color, input);
}
