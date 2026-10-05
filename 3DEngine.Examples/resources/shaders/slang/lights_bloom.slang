// raylib's lights_bloom.vs and lights_bloom.fs, written in Slang for the model pass: eight colored
// point lights, each fading with distance, lighting the surface with Blinn-Phong's highlight, the
// light left as it adds up for the post pass to bloom and tone map.
import modelpass;

// Input uniform values
uniform float3 viewPos;
uniform float3 lightPositions[8];
uniform float3 lightColors[8];

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texture as raylib reads it, encoded, where the pass decodes it
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float3 norm = normalize(input.normal);
    float3 viewDir = normalize(viewPos - input.world);

    float3 ambient = 0.05*texColor.rgb;
    float3 totalDiffuse = float3(0.0);
    float3 totalSpecular = float3(0.0);

    for (int i = 0; i < 8; i++)
    {
        float3 lightDir = normalize(lightPositions[i] - input.world);
        float dist = length(lightPositions[i] - input.world);
        float attenuation = 1.0/(1.0 + 0.8*dist + 0.4*dist*dist);

        // Diffuse shading
        float diff = max(dot(norm, lightDir), 0.0);
        totalDiffuse += diff*lightColors[i]*attenuation*1.2;

        // Blinn-Phong specular
        float3 halfwayDir = normalize(lightDir + viewDir);
        float spec = pow(max(dot(norm, halfwayDir), 0.0), 32.0);
        totalSpecular += spec*lightColors[i]*attenuation*0.8;
    }

    float3 result = ambient + (totalDiffuse*texColor.rgb) + totalSpecular;
    return float4(result, texColor.a);
}
