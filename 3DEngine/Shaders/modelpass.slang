// What the model pass gives every shader drawn with it, imported by model.slang and by a shader a
// program assigns to a model's material:
//
//     import modelpass;
//
//     uniform float4 rimColor;
//
//     [shader("fragment")]
//     float4 fragmentMain(ModelVertexOutput input) : SV_Target
//     {
//         float4 color = baseColor(input);
//         return float4(lit(color.rgb, input), color.a) + rimColor;
//     }
//
// Uniforms a shader declares at the top level are set by name with SetShaderValue. A shader with no
// vertexMain uses the one in model.slang.
module modelpass;

public struct ModelPushConstants
{
    public float4x4 transform;
    // The world matrix as three rows of a 3x4: the 3x3 turns normals into world space and the
    // fourth column is the translation, for the world position point lights need. A full second
    // matrix would take the block past the 128 bytes every device guarantees.
    public float4 worldX;
    public float4 worldY;
    public float4 worldZ;
    public float4 color;
};

[[vk::push_constant]]
public ConstantBuffer<ModelPushConstants> push;

[[vk::binding(1, 0)]]
public Sampler2D boundTexture;

// One light, packed by LightingUboPacker: position and kind, direction and radius, emitted
// color, shape, and the cone of a spot.
public struct Light
{
    public float4 positionAndType;
    public float4 directionAndRadius;
    public float4 emittedColorAndShadow;
    public float4 shapeParams;
    public float4 shapingParams;
};

// Three separate pads rather than an int3, which std140 would align to 16 bytes and so move the
// lights 16 bytes past where LightingUbo puts them.
public struct Lighting
{
    public int count;
    public int pad0;
    public int pad1;
    public int pad2;
    public Light lights[16];
};

[[vk::binding(0, 1)]]
public ConstantBuffer<Lighting> lighting;

public struct ModelVertexOutput
{
    public float4 position : SV_Position;
    public float3 normal : NORMAL;
    public float3 world : POSITION1;
    public float2 uv : TEXCOORD0;
};

// The kinds of LightType that light differently from a point.
static const int Distant = 0;
static const int Dome = 5;

// The vertex stage every model shader uses unless it writes its own.
public ModelVertexOutput transformModelVertex(float3 position, float3 normal, float2 uv)
{
    ModelVertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.normal = float3(dot(push.worldX.xyz, normal), dot(push.worldY.xyz, normal), dot(push.worldZ.xyz, normal));
    output.world = float3(dot(push.worldX, float4(position, 1.0)), dot(push.worldY, float4(position, 1.0)), dot(push.worldZ, float4(position, 1.0)));
    output.uv = uv;
    return output;
}

// The material's color times its texture at this point.
public float4 baseColor(ModelVertexOutput input)
{
    return push.color * boundTexture.Sample(input.uv);
}

// Light arriving at a point with a normal from one light: Lambert's cosine, with the square of the
// distance for anything that is not distant and the cone's edge for a spot.
float3 arriving(Light light, float3 world, float3 n)
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
    float result = max(dot(n, l), 0.0) / distanceSquared;

    float cone = light.shapeParams.w;
    if (cone > 0.0)
    {
        // Full inside the cone less its soft band, nothing outside it.
        float outer = cos(radians(cone));
        float inner = cos(radians(cone * (1.0 - saturate(light.shapingParams.x))));
        result *= smoothstep(outer, max(inner, outer + 1e-4), dot(-l, light.directionAndRadius.xyz));
    }
    return color * result;
}

// A color lit at this point: by the frame's light entities, or by one fixed light from above over
// an ambient floor when there are none.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    float3 n = normalize(input.normal);
    if (lighting.count == 0)
    {
        float3 toLight = normalize(float3(0.4, 1.0, 0.3));
        return albedo * (0.35 + 0.65 * max(dot(n, toLight), 0.0));
    }

    float3 light = float3(0.0);
    for (int i = 0; i < min(lighting.count, 16); i++)
        light += arriving(lighting.lights[i], input.world, n);
    return saturate(albedo * light);
}
