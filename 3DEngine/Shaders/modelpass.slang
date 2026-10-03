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
//
// Color is linear inside the pass and sRGB-encoded where it leaves it. baseColor gives linear
// color, lit takes linear color and gives it encoded, ready to return, and toDisplay encodes a
// linear color a shader works out itself. A color a shader returns as it is, such as a uniform
// set from the program, is taken as already encoded, as the 2D passes take every color.
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
    // The material in the last 16 bytes, which is its color as four bytes (red lowest), how
    // metallic and how rough it is, and how strongly its normal map bends the surface, 0 for none.
    public uint packedColor;
    public float metallic;
    public float roughness;
    public float normalScale;
};

[[vk::push_constant]]
public ConstantBuffer<ModelPushConstants> push;

[[vk::binding(1, 0)]]
public Sampler2D boundTexture;

// The material's tangent-space normal map, read only when its normal scale is above 0.
[[vk::binding(2, 0)]]
public Sampler2D normalMap;

// The material's metallic-roughness map as glTF packs it, roughness in green and metallic in
// blue, multiplying its factors. White when it has none.
[[vk::binding(3, 0)]]
public Sampler2D metallicRoughnessMap;

// sRGB's transfer function, exactly rather than as a power of 2.2, so a byte decoded and encoded
// again comes back as itself.
public float3 srgbToLinear(float3 c)
{
    return select(c <= 0.04045, c / 12.92, pow((max(c, 0.0) + 0.055) / 1.055, 2.4));
}

public float3 linearToSrgb(float3 c)
{
    c = saturate(c);
    return select(c <= 0.0031308, c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055);
}

// Linear color as the frame stores it, tonemapped first, since a target holds sRGB-encoded bytes.
public float3 toDisplay(float3 linear)
{
    return linearToSrgb(tonemap(linear));
}

// The material's color, unpacked from its four bytes and decoded to linear. Its alpha is linear
// as it is.
public float4 materialColor()
{
    uint c = push.packedColor;
    float4 bytes = float4(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF, c >> 24) / 255.0;
    return float4(srgbToLinear(bytes.rgb), bytes.a);
}

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

// The material's color times its texture at this point, in linear color. The texture is bound
// through a view that decodes it from sRGB as it is sampled, so filtering blends linear values.
public float4 baseColor(ModelVertexOutput input)
{
    return materialColor() * boundTexture.Sample(input.uv);
}

// The surface's normal at this point, bent by the normal map when the material has one. The map's
// tangent frame is worked out per pixel from how the position and texture coordinates change
// across the screen (Christian Schüler's cotangent frame), so a mesh needs no tangents. Up in the
// map is toward the top of the image, which is falling v.
public float3 surfaceNormal(ModelVertexOutput input)
{
    float3 n = normalize(input.normal);
    if (push.normalScale <= 0.0)
        return n;

    float3 dp1 = ddx(input.world);
    float3 dp2 = ddy(input.world);
    float2 duv1 = ddx(input.uv);
    float2 duv2 = ddy(input.uv);
    float3 dp2perp = cross(dp2, n);
    float3 dp1perp = cross(n, dp1);
    // The frame over the determinant's sign rather than its size, which the scale below takes
    // out, since a projection that mirrors the screen flips it.
    float flip = dot(dp1, dp2perp) < 0.0 ? -1.0 : 1.0;
    float3 t = (dp2perp * duv1.x + dp1perp * duv2.x) * flip;
    float3 b = -(dp2perp * duv1.y + dp1perp * duv2.y) * flip;
    float scale = max(dot(t, t), dot(b, b));
    if (scale <= 1e-20)
        return n;

    float3 mapped = normalMap.Sample(input.uv).xyz * 2.0 - 1.0;
    mapped.xy *= push.normalScale;
    float s = rsqrt(scale);
    return normalize(t * s * mapped.x + b * s * mapped.y + n * mapped.z);
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

static const float PI = 3.14159265;

// How much of a light reflects toward the eye off microfacets of this roughness, as GGX's
// distribution and Smith's height-correlated shadowing have it, with Schlick's Fresnel. Times pi,
// since a light here is given as the light a white diffuse surface facing it returns, so the
// diffuse term is the albedo itself rather than the albedo over pi.
float3 specularReflected(float3 n, float3 v, float3 l, float3 f0, float alpha, out float3 fresnel)
{
    float3 h = normalize(v + l);
    float nl = max(dot(n, l), 1e-4);
    float nv = max(dot(n, v), 1e-4);
    float nh = max(dot(n, h), 0.0);
    float a2 = alpha * alpha;
    float d = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / (PI * d * d);
    float visibility = 0.5 / (nl * sqrt(nv * nv * (1.0 - a2) + a2) + nv * sqrt(nl * nl * (1.0 - a2) + a2));
    fresnel = f0 + (1.0 - f0) * pow(1.0 - max(dot(v, h), 0.0), 5.0);
    return distribution * visibility * fresnel * PI;
}

// The share of light from all around that a surface reflects, with no environment to reflect,
// as Brian Karis fitted it for mobile, so a smooth or metal surface under an ambient light is not
// black.
float3 ambientReflected(float3 f0, float roughness, float nv)
{
    float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;
    return f0 * ab.x + ab.y;
}

// A linear color lit at this point, given sRGB-encoded for the frame. With light entities it is
// each light reflected by the material's metallic-roughness model, the shadowed light cut by its
// shadow map. With none it is one fixed light from above over an ambient floor and no highlight,
// which is how the flat API's models look. Either goes through the same tonemap and encoding.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    float3 n = surfaceNormal(input);
    if (lighting.count == 0)
    {
        float3 toLight = normalize(float3(0.4, 1.0, 0.3));
        // The ramp from 0.35 to 1 the light had when the pass worked in sRGB, raised to 2.2 so it
        // encodes back to the same shades and the flat API's models look as they did.
        return toDisplay(albedo * pow(0.35 + 0.65 * max(dot(n, toLight), 0.0), 2.2));
    }

    float4 packed = metallicRoughnessMap.Sample(input.uv);
    float metallic = saturate(push.metallic * packed.b);
    float roughness = clamp(push.roughness * packed.g, 0.045, 1.0);
    float alpha = roughness * roughness;
    float3 f0 = lerp(float3(0.04), albedo, metallic);
    float3 diffuseColor = albedo * (1.0 - metallic);

    float3 v = normalize(input.toEye);
    float nv = max(dot(n, v), 1e-4);
    float3 color = float3(0.0);
    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light light = lighting.lights[i];
        float3 arrived = arriving(light, input.world, n);
        if (int(light.positionAndKind.w) == Ambient)
        {
            color += arrived * (diffuseColor + ambientReflected(f0, roughness, nv));
            continue;
        }

        if (i == lighting.shadowLight)
            arrived *= lightReaching(input.world, normalize(input.normal));
        float3 l = -fromLight(light, input.world);
        float3 fresnel;
        float3 specular = specularReflected(n, v, l, f0, alpha, fresnel);
        // What the surface reflects at its face is not there to scatter diffusely.
        color += arrived * (diffuseColor * (1.0 - fresnel) + specular);
    }
    return toDisplay(color);
}
