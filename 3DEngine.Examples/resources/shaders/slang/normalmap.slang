// raylib's normalmap.vs and normalmap.fs, written in Slang for the model pass: one white point
// light and its highlight, over a surface bent by the material's normal map or left flat. raylib
// builds the map's frame from the mesh's tangents, and the pass works it out at each pixel instead,
// in surfaceNormal, as it does for every material with a normal map.
import modelpass;

uniform float3 viewPos;
uniform float3 lightPos;
uniform int useNormalMap;
uniform float specularExponent;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texel and the material's color as raylib reads them, encoded, where the pass decodes them
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texelColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 tint = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);

    float3 specular = float3(0.0);
    float3 viewDir = normalize(viewPos - input.world);
    float3 lightDir = normalize(lightPos - input.world);

    float3 normal;
    if (useNormalMap != 0)
    {
        normal = surfaceNormal(input);
    }
    else
    {
        normal = normalize(input.normal);
    }

    float3 lightColor = float3(1.0, 1.0, 1.0);
    float NdotL = max(dot(normal, lightDir), 0.0);
    float3 lightDot = lightColor*NdotL;

    float specCo = 0.0;

    if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewDir, reflect(-lightDir, normal))), specularExponent);

    specular += specCo;

    float4 finalColor = (texelColor*((tint + float4(specular, 1.0))*float4(lightDot, 1.0)));
    finalColor += texelColor*(float4(1.0, 1.0, 1.0, 1.0)/40.0)*tint;

    // Gamma correction
    return pow(finalColor, float4(1.0/2.2));
}
