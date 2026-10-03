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

// One light, packed by LightingUboPacker: position and kind, direction and range, color times
// intensity, and the cosines of a spot's inner and outer angles.
public struct Light
{
    public float4 positionAndKind;
    public float4 directionAndRange;
    public float4 colorAndShadow;
    public float4 cone;
};

// Laid out as LightingUbo is: the count, the shadowed light's index or -1, the world width of a
// shadow map texel and a pad, so the matrix starts 16 bytes in, then the lights.
public struct Lighting
{
    public int count;
    public int shadowLight;
    public float shadowTexel;
    public int pad0;
    public float4x4 shadowViewProjection;
    public Light lights[16];
};

[[vk::binding(0, 1)]]
public ConstantBuffer<Lighting> lighting;

// The depths the shadowed light sees, or a white texture, which reads as nothing in shadow.
[[vk::binding(1, 1)]]
public Sampler2D shadowMap;

public struct ModelVertexOutput
{
    public float4 position : SV_Position;
    public float3 normal : NORMAL;
    public float3 world : POSITION1;
    public float2 uv : TEXCOORD0;
    // From the surface toward the camera, unnormalized, for specular light.
    public float3 toEye : TEXCOORD1;
};

// Where the camera is, worked out from the transform alone, since the push constants have no
// room for it. Under a perspective projection the eye is the one point clip space's x, y and w all
// take as zero, three planes whose meeting point Cramer's rule gives. An orthographic projection
// has no such point, and gives the way toward the camera instead, with w of 0.
float4 eyeInModelSpace()
{
    float4 a = push.transform[0];
    float4 b = push.transform[1];
    float4 c = push.transform[3];
    float3 bc = cross(b.xyz, c.xyz);
    float det = dot(a.xyz, bc);
    if (abs(det) > 1e-8)
        return float4((-a.w * bc - b.w * cross(c.xyz, a.xyz) - c.w * cross(a.xyz, b.xyz)) / det, 1.0);

    // Toward the camera is where depth falls, which is against the transform's depth row.
    float3 back = normalize(cross(a.xyz, b.xyz));
    return float4(dot(back, push.transform[2].xyz) > 0.0 ? -back : back, 0.0);
}

// LightKind's values.
static const int Directional = 0;
static const int Point = 1;
static const int Spot = 2;
static const int Ambient = 3;

// The vertex stage every model shader uses unless it writes its own.
public ModelVertexOutput transformModelVertex(float3 position, float3 normal, float2 uv)
{
    ModelVertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.normal = float3(dot(push.worldX.xyz, normal), dot(push.worldY.xyz, normal), dot(push.worldZ.xyz, normal));
    output.world = float3(dot(push.worldX, float4(position, 1.0)), dot(push.worldY, float4(position, 1.0)), dot(push.worldZ, float4(position, 1.0)));
    output.uv = uv;

    float4 eye = eyeInModelSpace();
    float3 eyeWorld = float3(dot(push.worldX, eye), dot(push.worldY, eye), dot(push.worldZ, eye));
    output.toEye = eye.w == 0.0 ? eyeWorld : eyeWorld - output.world;
    return output;
}

// The material's color times its texture at this point.
public float4 baseColor(ModelVertexOutput input)
{
    return push.color * boundTexture.Sample(input.uv);
}

// Light arriving at a point with a normal from one light: Lambert's cosine, divided by the square
// of the distance for a point or a spot, brought smoothly to nothing at its range, and cut to a
// spot's cone.
float3 arriving(Light light, float3 world, float3 n)
{
    int kind = int(light.positionAndKind.w);
    float3 color = light.colorAndShadow.xyz;
    if (kind == Ambient)
        return color;
    if (kind == Directional)
        return color * max(dot(n, -light.directionAndRange.xyz), 0.0);

    float3 toLight = light.positionAndKind.xyz - world;
    float distanceSquared = max(dot(toLight, toLight), 1e-4);
    float3 l = toLight * rsqrt(distanceSquared);
    float result = max(dot(n, l), 0.0) / distanceSquared;

    float range = light.directionAndRange.w;
    if (range > 0.0)
    {
        // Full near the light and nothing at the range, without the step a plain cut leaves.
        float edge = saturate(1.0 - pow(distanceSquared / (range * range), 2.0));
        result *= edge * edge;
    }

    if (kind == Spot)
        result *= smoothstep(light.cone.y, max(light.cone.x, light.cone.y + 1e-4), dot(-l, light.directionAndRange.xyz));
    return color * result;
}

// How much of the shadowed light reaches this point, from 0 to 1. The point is moved off its
// surface along the normal by a texel and a half before it is compared, so a surface does not
// shadow itself where the map's texels slant across it, and nine depths around it are compared and
// averaged, which softens the stair steps of the map's texels into a gradient one texel wide.
float lightReaching(float3 world, float3 n)
{
    float4 clip = mul(lighting.shadowViewProjection, float4(world + n * lighting.shadowTexel * 1.5, 1.0));
    float3 p = clip.xyz / clip.w;
    float2 uv = p.xy * 0.5 + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0) || p.z > 1.0)
        return 1.0;

    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.0005 <= shadowMap.SampleLevel(uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// The way light comes from one light to a point, for specular.
float3 fromLight(Light light, float3 world)
{
    int kind = int(light.positionAndKind.w);
    return kind == Directional ? light.directionAndRange.xyz : normalize(world - light.positionAndKind.xyz);
}

// Brings light past 1 back under it. The brightest channel is left alone up to 0.9 and bends
// smoothly toward 1 past it, and the other two are scaled with it, so a bright orange light stays
// orange where a clamp per channel turns it white. Below the bend the color is unchanged, so a
// model looks the same lit by its first light entity as by the fixed light, which goes through
// the same curve.
public float3 tonemap(float3 color)
{
    static const float knee = 0.9;
    float peak = max(color.r, max(color.g, color.b));
    if (peak <= knee)
        return max(color, 0.0);
    float bent = knee + (1.0 - knee) * (1.0 - exp(-(peak - knee) / (1.0 - knee)));
    return max(color, 0.0) * (bent / peak);
}

// A color lit at this point. With light entities it is their diffuse light, the shadowed one's
// cut by its shadow map, and a Blinn-Phong highlight of each light that is not ambient. With none
// it is one fixed light from above over an ambient floor and no highlight, which is how the flat
// API's models look. Either goes through the same tonemap.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    float3 n = normalize(input.normal);
    if (lighting.count == 0)
    {
        float3 toLight = normalize(float3(0.4, 1.0, 0.3));
        return tonemap(albedo * (0.35 + 0.65 * max(dot(n, toLight), 0.0)));
    }

    float3 v = normalize(input.toEye);
    float3 diffuse = float3(0.0);
    float3 specular = float3(0.0);
    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light light = lighting.lights[i];
        float3 arrived = arriving(light, input.world, n);
        if (i == lighting.shadowLight)
            arrived *= lightReaching(input.world, n);
        diffuse += arrived;
        if (int(light.positionAndKind.w) != Ambient)
        {
            float3 h = normalize(v - fromLight(light, input.world));
            specular += arrived * 0.25 * pow(max(dot(n, h), 0.0), 32.0);
        }
    }
    return tonemap(albedo * diffuse + specular);
}
