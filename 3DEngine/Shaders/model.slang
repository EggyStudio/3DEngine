// Draws a mesh from the model pass: the texture times the color, lit by the lights the ECS holds,
// or by one fixed directional light over an ambient floor when it holds none, so a model reads as
// a solid before any lights exist.

struct PushConstants
{
    float4x4 transform;
    // The world matrix as three rows of a 3x4: the 3x3 turns normals into world space and the
    // fourth column is the translation, for the world position point lights need. A full second
    // matrix would take the block past the 128 bytes every device guarantees.
    float4 worldX;
    float4 worldY;
    float4 worldZ;
    float4 color;
};

// One light, packed by LightingUboPacker: position and kind, direction and radius, emitted
// color, shape, and the cone of a spot.
struct Light
{
    float4 positionAndType;
    float4 directionAndRadius;
    float4 emittedColorAndShadow;
    float4 shapeParams;
    float4 shapingParams;
};

// Three separate pads rather than an int3, which std140 would align to 16 bytes and so move the
// lights 16 bytes past where LightingUbo puts them.
struct Lighting
{
    int count;
    int pad0;
    int pad1;
    int pad2;
    Light lights[16];
};

// The kinds of LightType that light differently from a point.
static const int Distant = 0;
static const int Dome = 5;

[[vk::push_constant]]
ConstantBuffer<PushConstants> push;

[[vk::binding(1, 0)]]
Sampler2D boundTexture;

[[vk::binding(0, 1)]]
ConstantBuffer<Lighting> lighting;

struct VertexOutput
{
    float4 position : SV_Position;
    float3 normal : NORMAL;
    float3 world : POSITION1;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
{
    VertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.normal = float3(dot(push.worldX.xyz, normal), dot(push.worldY.xyz, normal), dot(push.worldZ.xyz, normal));
    output.world = float3(dot(push.worldX, float4(position, 1.0)), dot(push.worldY, float4(position, 1.0)), dot(push.worldZ, float4(position, 1.0)));
    output.uv = uv;
    return output;
}

// Light arriving at a point with a normal from one light: Lambert's cosine, with the square of the
// distance for anything that is not distant and the cone's edge for a spot.
float3 Arriving(Light light, float3 world, float3 n)
{
    int kind = int(light.positionAndType.w);
    float3 color = light.emittedColorAndShadow.xyz;
    if (kind == Dome)
        return color;
    if (kind == Distant)
        return color * max(dot(n, -light.directionAndRadius.xyz), 0.0);

    float3 toLight = light.positionAndType.xyz - world;
    float distanceSquared = max(dot(toLight, toLight), 1e-4);
    float3 l = toLight * rsqrt(distanceSquared);
    float arriving = max(dot(n, l), 0.0) / distanceSquared;

    float cone = light.shapeParams.w;
    if (cone > 0.0)
    {
        // Full inside the cone less its soft band, nothing outside it.
        float outer = cos(radians(cone));
        float inner = cos(radians(cone * (1.0 - saturate(light.shapingParams.x))));
        arriving *= smoothstep(outer, max(inner, outer + 1e-4), dot(-l, light.directionAndRadius.xyz));
    }
    return color * arriving;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = push.color * boundTexture.Sample(input.uv);
    if (color.a <= 0.0)
        discard;

    float3 n = normalize(input.normal);
    if (lighting.count == 0)
    {
        float3 toLight = normalize(float3(0.4, 1.0, 0.3));
        float shade = 0.35 + 0.65 * max(dot(n, toLight), 0.0);
        return float4(color.rgb * shade, color.a);
    }

    float3 light = float3(0.0);
    for (int i = 0; i < min(lighting.count, 16); i++)
        light += Arriving(lighting.lights[i], input.world, n);
    return float4(saturate(color.rgb * light), color.a);
}
