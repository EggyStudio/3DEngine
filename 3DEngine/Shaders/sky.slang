// Draws the environment map as a backdrop, on a cube around the camera that DrawSkybox records.
// Each pixel looks the sky up along the way from the eye through it, so the cube's size and place
// never show, and its depth is set a millionth inside the far plane, so everything else draws in front.
import modelpass;

// The color SetSkyHorizon draws below the horizon, sRGB, with 1 in w where there is one, and in x
// of the second the degrees above the horizon over which the image blends into it.
uniform float4 horizonColor;
uniform float4 horizonBlend;

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
    float3 look = normalize(-input.toEye);
    float3 sky = skyMap.SampleLevel(lightLinear, look, 0).rgb * lighting.environment.x * input.color.rgb;
    if (horizonColor.w > 0.0)
    {
        // The image's share rises smoothly from none at the horizon to all of it at the blend's
        // top, so neither end shows as an edge, and below the horizon the color stands alone. The
        // two are mixed as light, as the program's fog mixes the land into the color.
        float above = degrees(asin(clamp(look.y, -1.0, 1.0)));
        float share = horizonBlend.x > 0.0 ? smoothstep(0.0, horizonBlend.x, above) : step(0.0, above);
        sky = lerp(srgbToLinear(horizonColor.rgb), sky, share);
    }
    return float4(toDisplay(sky), 1.0);
}
