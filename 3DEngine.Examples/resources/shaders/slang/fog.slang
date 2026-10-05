// raylib's lighting.vs and fog.fs, written in Slang for the model pass: raylib's lights, as
// shaders_basic_lighting has them, over the model's texture, then a fog that thickens with the
// distance from the camera.
import modelpass;

static const int MAX_LIGHTS = 4;
static const int LIGHT_DIRECTIONAL = 0;
static const int LIGHT_POINT = 1;

// raylib's Light, under a name apart from the model pass's own
struct ShaderLight
{
    int enabled;
    int type;
    float3 position;
    float3 target;
    float4 color;
};

// Input lighting values
uniform ShaderLight lights[MAX_LIGHTS];
uniform float4 ambient;
uniform float3 viewPos;
uniform float4 fogColor;
uniform float fogDensity;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texel and the material's color as raylib reads them, encoded, where the pass decodes them
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texelColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 colDiffuse = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);

    float3 lightDot = float3(0.0);
    float3 normal = normalize(input.normal);
    float3 viewD = normalize(viewPos - input.world);
    float3 specular = float3(0.0);

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled == 1)
        {
            float3 light = float3(0.0);

            if (lights[i].type == LIGHT_DIRECTIONAL) light = -normalize(lights[i].target - lights[i].position);
            if (lights[i].type == LIGHT_POINT) light = normalize(lights[i].position - input.world);

            float NdotL = max(dot(normal, light), 0.0);
            lightDot += lights[i].color.rgb*NdotL;

            float specCo = 0.0;
            if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 16.0); // Shine: 16.0
            specular += specCo;
        }
    }

    float4 finalColor = (texelColor*((colDiffuse + float4(specular, 1))*float4(lightDot, 1.0)));
    finalColor += texelColor*(ambient/10.0);

    // Gamma correction
    finalColor = pow(finalColor, float4(1.0/2.2));

    // Fog calculation
    float dist = length(viewPos - input.world);

    // Exponential fog
    float fogFactor = 1.0/exp((dist*fogDensity)*(dist*fogDensity));

    fogFactor = clamp(fogFactor, 0.0, 1.0);

    return lerp(fogColor, finalColor, fogFactor);
}
