// Draws the environment map as a backdrop, on a cube around the camera that DrawSkybox records.
// Each pixel looks the sky up along the way from the eye through it, so the cube's size and place
// never show, and its depth is set a millionth inside the far plane, so everything else draws in front.
import modelpass;

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    output.position.z = output.position.w * 0.999999;
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float3 sky = skyMap.SampleLevel(lightLinear, -input.toEye, 0).rgb * lighting.environment.x;
    return float4(toDisplay(sky * input.color.rgb), 1.0);
}
