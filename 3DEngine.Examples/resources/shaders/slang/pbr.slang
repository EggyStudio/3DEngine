// raylib's pbr.vs and pbr.fs, written in Slang for the model pass: Cook-Torrance's light from up to
// four point lights over albedo, metalness, roughness and occlusion maps, a normal map and an
// emissive one, tonemapped and corrected for gamma at the end. raylib's shader reads its maps by
// names of its own, and here they are the material's maps the pass binds: the albedo map is the
// material's texture, the MRA map its metallic-roughness map, read with raylib's channels.
import modelpass;

static const int MAX_LIGHTS = 4;
static const int LIGHT_DIRECTIONAL = 0;
static const int LIGHT_POINT = 1;
static const float PI = 3.14159265358979323846;

// raylib's Light, under a name apart from the model pass's own
struct ShaderLight
{
    int enabled;
    int type;
    float3 position;
    float3 target;
    float4 color;
    float intensity;
};

uniform int numOfLights;

uniform float2 tiling;
uniform float2 offset;

uniform int useTexAlbedo;
uniform int useTexNormal;
uniform int useTexMRA;
uniform int useTexEmissive;

uniform float4 emissiveColor;
uniform float normalValue;
uniform float metallicValue;
uniform float roughnessValue;
uniform float aoValue;
uniform float emissivePower;

// Input lighting values
uniform ShaderLight lights[MAX_LIGHTS];
uniform float3 viewPos;

uniform float3 ambientColor;
uniform float ambient;

// Reflectivity in range 0.0 to 1.0, increased when the surface is seen at a larger angle
float3 SchlickFresnel(float hDotV, float3 refl)
{
    return refl + (1.0 - refl)*pow(1.0 - hDotV, 5.0);
}

float GgxDistribution(float nDotH, float roughness)
{
    float a = roughness*roughness*roughness*roughness;
    float d = nDotH*nDotH*(a - 1.0) + 1.0;
    d = PI*d*d;
    return (a/max(d, 0.0000001));
}

float GeomSmith(float nDotV, float nDotL, float roughness)
{
    float r = roughness + 1.0;
    float k = r*r/8.0;
    float ik = 1.0 - k;
    float ggx1 = nDotV/(nDotV*ik + k);
    float ggx2 = nDotL/(nDotL*ik + k);
    return ggx1*ggx2;
}

float3 ComputePBR(ModelVertexOutput input)
{
    // raylib's vertex stage doubles the texture coordinates, which a tiling of a half takes back.
    float2 uv = float2(input.uv.x*2.0*tiling.x + offset.x, input.uv.y*2.0*tiling.y + offset.y);

    // The albedo, the material's color and the emissive map as raylib reads them, encoded, where
    // the pass decodes them
    float3 albedo = linearToSrgb(boundTexture.Sample(uv).rgb);
    float4 albedoColor = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);
    albedo = float3(albedoColor.x*albedo.x, albedoColor.y*albedo.y, albedoColor.z*albedo.z);

    float metallic = clamp(metallicValue, 0.0, 1.0);
    float roughness = clamp(roughnessValue, 0.0, 1.0);
    float ao = clamp(aoValue, 0.0, 1.0);

    if (useTexMRA == 1)
    {
        float4 mra = metallicRoughnessMap.Sample(uv);
        metallic = clamp(mra.r + metallicValue, 0.04, 1.0);
        roughness = clamp(mra.g + roughnessValue, 0.04, 1.0);
        ao = (mra.b + aoValue)*0.5;
    }

    float3 N = normalize(input.normal);
    if (useTexNormal == 1)
    {
        // The normal map's frame comes from the tiled coordinates, as raylib's tangents follow them.
        ModelVertexOutput tiled = input;
        tiled.uv = uv;
        N = surfaceNormal(tiled);
    }

    float3 V = normalize(viewPos - input.world);

    float3 emissive = float3(0);
    emissive = linearToSrgb(emissiveMap.Sample(uv).rgb).g*emissiveColor.rgb*emissivePower*useTexEmissive;

    // A dielectric reflects 0.04 of the light head on, and a metal its albedo.
    float3 baseRefl = lerp(float3(0.04), albedo.rgb, metallic);
    float3 lightAccum = float3(0.0);  // Accumulated light

    for (int i = 0; i < numOfLights; i++)
    {
        float3 L = normalize(lights[i].position - input.world);     // Compute light vector
        float3 H = normalize(V + L);                                 // Compute halfway bisecting vector
        float dist = length(lights[i].position - input.world);      // Compute distance to light
        float attenuation = 1.0/(dist*dist*0.23);                    // Compute attenuation
        float3 radiance = lights[i].color.rgb*lights[i].intensity*attenuation; // Light energy coming in

        // Cook-Torrance BRDF distribution function
        float nDotV = max(dot(N, V), 0.0000001);
        float nDotL = max(dot(N, L), 0.0000001);
        float hDotV = max(dot(H, V), 0.0);
        float nDotH = max(dot(N, H), 0.0);
        float D = GgxDistribution(nDotH, roughness);    // Larger the more micro-facets aligned to H
        float G = GeomSmith(nDotV, nDotL, roughness);   // Smaller the more micro-facets shadow
        float3 F = SchlickFresnel(hDotV, baseRefl);     // Fresnel proportion of specular reflectance

        float3 spec = (D*G*F)/(4.0*nDotV*nDotL);

        // Diffuse and specular light add up to no more than 1, the diffuse being what the
        // specular leaves, and only non-metals have diffuse light.
        float3 kD = float3(1.0) - F;
        kD *= 1.0 - metallic;
        lightAccum += ((kD*albedo.rgb/PI + spec)*radiance*nDotL)*lights[i].enabled; // Angle of light has impact on result
    }

    float3 ambientFinal = (ambientColor + albedo)*ambient*0.5;

    return (ambientFinal + lightAccum*ao + emissive);
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float3 color = ComputePBR(input);

    // HDR tonemapping
    color = pow(color, color + float3(1.0));

    // Gamma correction
    color = pow(color, float3(1.0/2.2));

    return float4(color, 1.0);
}
