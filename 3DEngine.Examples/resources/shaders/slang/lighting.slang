// raylib's lighting.vs and lighting.fs, written in Slang for shapes drawn in 3D: up to four lights,
// each a point or a direction, Phong's highlight on each, and a little ambient light, all corrected
// for gamma at the end. raylib's shapes carry a normal at each vertex, and the immediate pass's
// vertices carry none, so a face's normal is found from how the position changes across the screen,
// flat over the face as the normals of raylib's planes and cubes are.
import engine;

static const int MAX_LIGHTS = 4;
static const int LIGHT_DIRECTIONAL = 0;
static const int LIGHT_POINT = 1;

struct Light
{
    int enabled;
    int type;
    float3 position;
    float3 target;
    float4 color;
};

// Input lighting values
uniform Light lights[MAX_LIGHTS];
uniform float4 ambient;
uniform float3 viewPos;

struct LightingOutput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
    float3 world : TEXCOORD1;
};

[shader("vertex")]
LightingOutput vertexMain(float3 position : POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0)
{
    VertexOutput transformed = transformVertex(position, uv, color);

    LightingOutput output;
    output.position = transformed.position;
    output.uv = uv;
    output.color = color;
    // A shape's vertices are already in the world, as raylib's are with matModel the identity.
    output.world = position;
    return output;
}

[shader("fragment")]
float4 fragmentMain(LightingOutput input) : SV_Target
{
    // Texel color fetching from texture sampler
    float4 texelColor = boundTexture.Sample(input.uv);
    float3 lightDot = float3(0.0);
    float3 normal = normalize(cross(ddy(input.world), ddx(input.world)));
    float3 viewD = normalize(viewPos - input.world);
    float3 specular = float3(0.0);

    float4 tint = input.color;

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
                light = normalize(lights[i].position - input.world);
            }

            float NdotL = max(dot(normal, light), 0.0);
            lightDot += lights[i].color.rgb*NdotL;

            float specCo = 0.0;
            if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 16.0); // 16 refers to shine
            specular += specCo;
        }
    }

    float4 finalColor = (texelColor*((tint + float4(specular, 1.0))*float4(lightDot, 1.0)));
    finalColor += texelColor*(ambient/10.0)*tint;

    // Gamma correction
    return pow(finalColor, float4(1.0/2.2));
}
