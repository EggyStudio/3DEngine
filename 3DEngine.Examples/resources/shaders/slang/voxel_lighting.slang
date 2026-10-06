// raylib's voxel_lighting.vs and voxel_lighting.fs, written in Slang for the model pass: up to four
// lights over each voxel's color, the mesh's color at each vertex, Phong's highlight on each and a
// little ambient light, corrected for gamma at the end.
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

[shader("fragment")]
float4 fragmentMain(ModelStreamsOutput input) : SV_Target
{
    // The mesh's color and the material's as raylib reads them, encoded, where the pass decodes them
    float4 fragColor = float4(linearToSrgb(input.vertexColor.rgb), input.vertexColor.a);
    float4 colDiffuse = float4(linearToSrgb(materialColor(input.base).rgb), materialColor(input.base).a);

    float3 lightDot = float3(0.0);
    float3 normal = normalize(input.base.normal);
    float3 viewD = normalize(viewPos - input.base.world);
    float3 specular = float3(0.0);

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled == 1)
        {
            float3 light = float3(0.0);

            if (lights[i].type == LIGHT_DIRECTIONAL)
            {
                light = -normalize(lights[i].target - lights[i].position);
            }

            if (lights[i].type == LIGHT_POINT)
            {
                light = normalize(lights[i].position - input.base.world);
            }

            float NdotL = max(dot(normal, light), 0.0);
            lightDot += lights[i].color.rgb*NdotL;

            float specCo = 0.0;
            if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 16.0); // 16 refers to shine
            specular += specCo;
        }
    }

    float4 finalColor = (fragColor*((colDiffuse + float4(specular, 1.0))*float4(lightDot, 1.0)));
    finalColor += fragColor*(ambient/10.0)*colDiffuse;

    // Gamma correction
    return pow(finalColor, float4(1.0/2.2));
}
