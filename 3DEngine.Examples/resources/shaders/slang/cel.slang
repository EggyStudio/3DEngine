// raylib's cel.vs and cel.fs, written in Slang for the model pass: the light falling on each point
// rounded down to one of a few bands, as a cartoon is shaded, over the model's texture and color.
import modelpass;

// raylib's Light, under a name apart from the model pass's own
struct ShaderLight
{
    int enabled;
    int type; // 0 = directional, 1 = point
    float3 position;
    float3 target;
    float4 color;
    float attenuation;
};

// View position for future specular / fresnel use.
uniform float3 viewPos;

// Number of discrete toon bands (2 = hard binary, 10 = default, 20 = near-smooth).
uniform float numBands;

// rlights.h compatible light block.
uniform ShaderLight lights[4];

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texel and the material's color as raylib reads them, encoded, where the pass decodes them
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 colDiffuse = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);

    float3 baseColor = texColor.rgb * colDiffuse.rgb;
    float3 norm = normalize(input.normal);

    float lightAccum = 0.08; // ambient floor

    for (int i = 0; i < 4; i++)
    {
        if (lights[i].enabled == 0) continue;

        float3 lightDir;
        if (lights[i].type == 0)
        {
            // Directional: direction is from position toward target.
            lightDir = normalize(lights[i].position - lights[i].target);
        }
        else
        {
            // Point: direction from surface to light.
            lightDir = normalize(lights[i].position - input.world);
        }

        float NdotL = max(dot(norm, lightDir), 0.0);

        // Quantize NdotL into numBands discrete steps.
        // min() guards against NdotL == 1.0 producing an out-of-range index.
        float quantized = min(floor(NdotL * numBands), numBands - 1.0) / (numBands - 1.0);
        lightAccum += quantized * lights[i].color.r;
    }

    lightAccum = clamp(lightAccum, 0.0, 1.0);
    return float4(baseColor * lightAccum, texColor.a * colDiffuse.a);
}
