// raylib's deferred_shading.vs and deferred_shading.fs, written in Slang: each pixel lit by up to
// four point lights from the position, normal, color and specular strength a G-buffer holds, with
// a little ambient light. It writes the G-buffer's depth as well, which raylib's example copies
// into the window's after it draws, so shapes drawn later are hidden behind the scene.
import engine;

Sampler2D gPosition;
Sampler2D gNormal;
Sampler2D gAlbedoSpec;
Sampler2D gDepth;

// raylib's Light, under a name apart from the engine's own
struct ShaderLight
{
    int enabled;
    int type;       // Unused in this demo
    float3 position;
    float3 target;    // Unused in this demo
    float4 color;
};

static const int NR_LIGHTS = 4;
uniform ShaderLight lights[NR_LIGHTS];
uniform float3 viewPosition;

static const float QUADRATIC = 0.032;
static const float LINEAR = 0.09;

struct DeferredOutput
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
DeferredOutput fragmentMain(VertexOutput input)
{
    float2 texCoord = input.uv;
    float3 fragPosition = gPosition.Sample(texCoord).rgb;
    float3 normal = gNormal.Sample(texCoord).rgb;
    float3 albedo = gAlbedoSpec.Sample(texCoord).rgb;
    float specular = gAlbedoSpec.Sample(texCoord).a;

    float3 ambient = albedo*float3(0.1f);
    float3 viewDirection = normalize(viewPosition - fragPosition);

    for (int i = 0; i < NR_LIGHTS; i++)
    {
        if (lights[i].enabled == 0) continue;
        float3 lightDirection = lights[i].position - fragPosition;
        float3 diffuse = max(dot(normal, lightDirection), 0.0)*albedo*lights[i].color.xyz;

        float3 halfwayDirection = normalize(lightDirection + viewDirection);
        float spec = pow(max(dot(normal, halfwayDirection), 0.0), 32.0);
        float3 specularColor = specular*spec*lights[i].color.xyz;

        // Attenuation
        float distance = length(lights[i].position - fragPosition);
        float attenuation = 1.0/(1.0 + LINEAR*distance + QUADRATIC*distance*distance);
        diffuse *= attenuation;
        specularColor *= attenuation;
        ambient += diffuse + specularColor;
    }

    DeferredOutput output;
    output.color = float4(ambient, 1.0);
    output.depth = gDepth.Sample(texCoord).r;
    return output;
}
