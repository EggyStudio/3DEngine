// raylib's outline_hull.vs and outline_hull.fs, written in Slang for the model pass: the model
// pushed out along its normals and drawn dark, for an outline where its front faces are culled.
import modelpass;

uniform float outlineThickness;

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    // Extrude vertex along its normal to create the hull.
    float3 extruded = position + normal * outlineThickness;
    return transformModelVertex(extruded, normal, uv, instance);
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    return float4(0.05, 0.05, 0.05, 1.0);
}
