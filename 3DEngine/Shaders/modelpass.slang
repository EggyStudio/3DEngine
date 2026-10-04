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

// One drawn copy of a mesh, read from a vertex buffer stepped once per instance, so every mesh
// sharing a mesh and its maps is drawn by one call. A vertex stage of a shader's own takes it as a
// parameter after the mesh's three and hands it to transformModelVertex.
public struct ModelInstance
{
    // The model to clip space transform, by its rows as System.Numerics lays them out.
    public float4 transform0 : INSTANCE_TRANSFORM0;
    public float4 transform1 : INSTANCE_TRANSFORM1;
    public float4 transform2 : INSTANCE_TRANSFORM2;
    public float4 transform3 : INSTANCE_TRANSFORM3;
    // The world matrix as three rows of a 3x4: the 3x3 turns normals into world space and the
    // fourth column is the translation, for the world position point lights need.
    public float4 worldX : INSTANCE_WORLDX;
    public float4 worldY : INSTANCE_WORLDY;
    public float4 worldZ : INSTANCE_WORLDZ;
    // The material's factors. Color and emission are linear, decoded on the CPU, and emission
    // may pass 1.
    public float4 color : INSTANCE_COLOR;
    public float4 emission : INSTANCE_EMISSION;
    // Metallic, roughness, normal scale (0 when the material has no normal map) and occlusion strength.
    public float4 factors : INSTANCE_FACTORS;
};

[[vk::binding(1, 0)]]
public Sampler2D boundTexture;

// The material's tangent-space normal map, read only when its normal scale is above 0.
[[vk::binding(2, 0)]]
public Sampler2D normalMap;

// The material's metallic-roughness map as glTF packs it, roughness in green and metallic in
// blue, multiplying its factors. White when it has none.
[[vk::binding(3, 0)]]
public Sampler2D metallicRoughnessMap;

// The light the material gives off, as a color multiplying its emission, decoded from sRGB as it
// is sampled. White when it has none.
[[vk::binding(4, 0)]]
public Sampler2D emissiveMap;

// How much light from all around reaches into the material's creases, in red, as glTF has it.
// White when it has none.
[[vk::binding(5, 0)]]
public Sampler2D occlusionMap;

// The light the material gives off at this point, linear, which no light entity is needed for.
public float3 emitted(ModelVertexOutput input)
{
    return materialEmission(input) * emissiveMap.Sample(input.uv).rgb;
}

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

// The material's factors at this point, which its instance carried through the vertex stage.
public float4 materialColor(ModelVertexOutput input) { return input.color; }
public float materialMetallic(ModelVertexOutput input) { return input.factors.x; }
public float materialRoughness(ModelVertexOutput input) { return input.factors.y; }
public float materialNormalScale(ModelVertexOutput input) { return input.factors.z; }
public float materialOcclusionStrength(ModelVertexOutput input) { return input.factors.w; }
public float3 materialEmission(ModelVertexOutput input) { return input.emission.xyz; }

// One light, packed by LightingUboPacker: position and kind, direction and range, color times
// intensity, and the cosines of a spot's inner and outer angles.
public struct Light
{
    public float4 positionAndKind;
    public float4 directionAndRange;
    public float4 colorAndShadow;
    public float4 cone;
};

// Laid out as LightingUbo is. The count, the shadowed light's index or -1, how many cascades it
// has and the shadowed spot light's index or -1 come first, then the world width of a texel of each cascade, the cascades'
// transforms nearest first, and the lights.
public struct Lighting
{
    public int count;
    public int shadowLight;
    public int cascadeCount;
    // How many spot lights cast shadows, drawn into the map's last tile, the whole of it for one
    // and a quarter each for more.
    public int spotShadowCount;
    // A texel's world width for each cascade, and in w the spot tile's per unit of distance.
    public float4 shadowTexels;
    public float4x4 shadowCascades[4];
    // The environment map's intensity, its last mip, and 1 when there is one.
    public float4 environment;
    public Light lights[16];
    // The width of a point light's shadow texel per unit of distance from it, in x.
    public float4 pointShadow;
    // World space to each face's clip space for each shadowed point light, six to a light in the
    // order +X, -X, +Y, -Y, +Z, -Z.
    public float4x4 pointShadowFaces[24];
    // The width of each shadowed spot light's texel per unit of distance from it, by slot.
    public float4 spotShadowTexels;
    // World space to each shadowed spot light's clip space, by slot.
    public float4x4 spotShadows[4];
};

[[vk::binding(0, 1)]]
public ConstantBuffer<Lighting> lighting;

// The depths the shadowed light sees, or a white texture, which reads as nothing in shadow.
[[vk::binding(1, 1)]]
public Sampler2D shadowMap;

// The light from all around, prefiltered by roughness down its mips, or a black cube for none.
[[vk::binding(2, 1)]]
public SamplerCube environmentMap;

// The same light unfiltered, at about its image's resolution, which sky.slang draws as a backdrop,
// or a black cube for none.
[[vk::binding(3, 1)]]
public SamplerCube skyMap;

// The depths each shadowed point light sees, a layer for each of its six faces.
[[vk::binding(4, 1)]]
public Sampler2DArray pointShadowMap;

public struct ModelVertexOutput
{
    public float4 position : SV_Position;
    public float3 normal : NORMAL;
    public float3 world : POSITION1;
    public float2 uv : TEXCOORD0;
    // From the surface toward the camera, unnormalized, for specular light.
    public float3 toEye : TEXCOORD1;
    // The instance's material factors, the same across the triangle.
    public nointerpolation float4 color : COLOR1;
    public nointerpolation float4 emission : COLOR2;
    public nointerpolation float4 factors : TEXCOORD2;
};

// Where the camera is, worked out from the transform alone, so an instance carries no more than
// it. The transform here is as mul takes it, its rows the columns System.Numerics has. Under a perspective projection the eye is the one point clip space's x, y and w all
// take as zero, three planes whose meeting point Cramer's rule gives. An orthographic projection
// has no such point, and gives the way toward the camera instead, with w of 0.
float4 eyeInModelSpace(float4x4 transform)
{
    float4 a = transform[0];
    float4 b = transform[1];
    float4 c = transform[3];
    float3 bc = cross(b.xyz, c.xyz);
    float det = dot(a.xyz, bc);
    if (abs(det) > 1e-8)
        return float4((-a.w * bc - b.w * cross(c.xyz, a.xyz) - c.w * cross(a.xyz, b.xyz)) / det, 1.0);

    // Toward the camera is where depth falls, which is against the transform's depth row.
    float3 back = normalize(cross(a.xyz, b.xyz));
    return float4(dot(back, transform[2].xyz) > 0.0 ? -back : back, 0.0);
}

// LightKind's values.
static const int Directional = 0;
static const int Point = 1;
static const int Spot = 2;
static const int Ambient = 3;

// The vertex stage every model shader uses unless it writes its own.
public ModelVertexOutput transformModelVertex(float3 position, float3 normal, float2 uv, ModelInstance instance)
{
    // The rows System.Numerics multiplies a row vector by are the columns of what mul takes.
    float4x4 transform = transpose(float4x4(instance.transform0, instance.transform1, instance.transform2, instance.transform3));
    ModelVertexOutput output;
    output.position = mul(transform, float4(position, 1.0));
    output.normal = float3(dot(instance.worldX.xyz, normal), dot(instance.worldY.xyz, normal), dot(instance.worldZ.xyz, normal));
    output.world = float3(dot(instance.worldX, float4(position, 1.0)), dot(instance.worldY, float4(position, 1.0)), dot(instance.worldZ, float4(position, 1.0)));
    output.uv = uv;

    float4 eye = eyeInModelSpace(transform);
    float3 eyeWorld = float3(dot(instance.worldX, eye), dot(instance.worldY, eye), dot(instance.worldZ, eye));
    output.toEye = eye.w == 0.0 ? eyeWorld : eyeWorld - output.world;
    output.color = instance.color;
    output.emission = instance.emission;
    output.factors = instance.factors;
    return output;
}

// The material's color times its texture at this point, in linear color. The texture is bound
// through a view that decodes it from sRGB as it is sampled, so filtering blends linear values.
public float4 baseColor(ModelVertexOutput input)
{
    return materialColor(input) * boundTexture.Sample(input.uv);
}

// The color as the material's alpha mode means it. Alpha is ignored for an opaque material, cut
// out below the cutoff for a masked one and solid above it, and kept for a blended one, whose
// clear parts are cut out so they leave the depth buffer alone. The mode rides in the
// instance's emission w.
public float4 alphaTested(float4 color, ModelVertexOutput input)
{
    float cutoff = input.emission.w;
    if (cutoff < 0.0)
        return float4(color.rgb, 1.0);
    if (cutoff > 0.0)
    {
        if (color.a < cutoff)
            discard;
        return float4(color.rgb, 1.0);
    }
    if (color.a <= 0.0)
        discard;
    return color;
}

// The surface's normal at this point, bent by the normal map when the material has one. The map's
// tangent frame is worked out per pixel from how the position and texture coordinates change
// across the screen (Christian Schüler's cotangent frame), so a mesh needs no tangents. Up in the
// map is toward the top of the image, which is falling v.
public float3 surfaceNormal(ModelVertexOutput input)
{
    float3 n = normalize(input.normal);
    // The back of a double-sided face is lit as its front, by the normal turned toward the viewer,
    // as glTF has it. A single-sided face's back is never drawn.
    if (dot(n, input.toEye) < 0.0)
        n = -n;
    float strength = materialNormalScale(input);
    if (strength <= 0.0)
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
    mapped.xy *= strength;
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

// How much of the shadowed light reaches this point through cascade c, from 0 to 1, or -1 when the
// cascade's tile does not hold it. Each cascade is a quarter of the map, two tiles on a side. The
// point is moved off its surface along the normal by a texel and a half of that cascade before it
// is compared, so a surface does not shadow itself where the map's texels slant across it, and
// nine depths around it are compared and averaged, which softens the stair steps of the texels into
// a gradient one texel wide. A point a little inside a tile's edge counts as outside, so the nine
// never reach into a neighbouring tile. edge says how far toward the tile's edge the point is, from
// 0 in its middle to 1 at that border.
float cascadeReached(int c, float3 world, float3 n, float2 texel, out float edge)
{
    edge = 1.0;
    float4 clip = mul(lighting.shadowCascades[c], float4(world + n * lighting.shadowTexels[c] * 1.5, 1.0));
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0)
        return -1.0;

    edge = max(abs(p.x), abs(p.y)) / 0.995;
    float2 uv = (p.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.0005 <= shadowMap.SampleLevel(uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// How much of the shadowed light reaches this point, from 0 to 1, from the nearest cascade whose
// tile holds it. Across the outer fifth of a tile the next cascade is read too and blended in, so
// where one cascade gives way to the next the shadow's softness changes over a band rather than in
// a step, and past the last cascade's band the shadow fades out rather than stopping at a line.
float lightReaching(float3 world, float3 n)
{
    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    for (int c = 0; c < lighting.cascadeCount; c++)
    {
        float edge;
        float reached = cascadeReached(c, world, n, texel, edge);
        if (reached < 0.0)
            continue;

        float blend = saturate((edge - 0.8) / 0.2);
        if (blend <= 0.0)
            return reached;
        float nextEdge;
        float next = c + 1 < lighting.cascadeCount ? cascadeReached(c + 1, world, n, texel, nextEdge) : -1.0;
        return lerp(reached, next < 0.0 ? 1.0 : next, blend);
    }
    return 1.0;
}

// How much of the shadowed spot light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from its square of the map's last tile, compared as the cascades are. Its
// texels widen with distance from the light, so the offset along the normal does too.
float spotLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float offset = lighting.spotShadowTexels[slot] * distance(world, lightPosition) * 1.5;
    float4 clip = mul(lighting.spotShadows[slot], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    // The last tile is the map's lower right quarter, split in four for more than one light.
    float size = lighting.spotShadowCount > 1 ? 0.25 : 0.5;
    float2 origin = float2(0.5, 0.5) + (lighting.spotShadowCount > 1 ? float2(slot % 2, slot / 2) * 0.25 : float2(0.0, 0.0));
    float2 uv = origin + (p.xy * 0.5 + 0.5) * size;
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= shadowMap.SampleLevel(uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// How much of the shadowed point light in slot (counted from 0) at lightPosition reaches this
// point, from 0 to 1, from the face of its six that the point lies furthest along the axis of,
// compared as the cascades are. Its texels widen with distance from the light, as a spot light's.
float pointLightReaching(float3 world, float3 n, float3 lightPosition, int slot)
{
    float3 away = world - lightPosition;
    float3 size = abs(away);
    int face = size.x >= size.y && size.x >= size.z ? (away.x >= 0.0 ? 0 : 1)
             : size.y >= size.z ? (away.y >= 0.0 ? 2 : 3)
             : (away.z >= 0.0 ? 4 : 5);
    float offset = lighting.pointShadow.x * length(away) * 1.5;
    float4 clip = mul(lighting.pointShadowFaces[slot * 6 + face], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height, layers;
    pointShadowMap.GetDimensions(width, height, layers);
    float2 texel = 1.0 / float2(width, height);
    float2 uv = p.xy * 0.5 + 0.5;
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= pointShadowMap.SampleLevel(float3(uv + float2(x, y) * texel, slot * 6 + face), 0.0).r ? 1.0 : 0.0;
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
// shadow map and the light from all around darkened by its occlusion map. With none it is one
// fixed light from above over an ambient floor and no highlight, which is how the flat API's
// models look. Either adds the light the material gives off, and goes through the same tonemap
// and encoding.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    float3 n = surfaceNormal(input);
    bool environment = lighting.environment.z > 0.0;
    if (lighting.count == 0 && !environment)
    {
        float3 toLight = normalize(float3(0.4, 1.0, 0.3));
        // The ramp from 0.35 to 1 the light had when the pass worked in sRGB, raised to 2.2 so it
        // encodes back to the same shades and the flat API's models look as they did.
        return toDisplay(albedo * pow(0.35 + 0.65 * max(dot(n, toLight), 0.0), 2.2) + emitted(input));
    }

    float4 packed = metallicRoughnessMap.Sample(input.uv);
    float metallic = saturate(materialMetallic(input) * packed.b);
    float roughness = clamp(materialRoughness(input) * packed.g, 0.045, 1.0);
    // Occlusion darkens the light from all around, which reaches into a crease less, and leaves
    // the direct light, which the shadow map takes care of.
    float occlusion = lerp(1.0, occlusionMap.Sample(input.uv).r, saturate(materialOcclusionStrength(input)));
    float alpha = roughness * roughness;
    float3 f0 = lerp(float3(0.04), albedo, metallic);
    float3 diffuseColor = albedo * (1.0 - metallic);

    float3 v = normalize(input.toEye);
    float nv = max(dot(n, v), 1e-4);
    float3 color = float3(0.0);

    // The environment, as an ambient light that differs by direction. The mirror direction is
    // looked up at the mip prefiltered for this roughness and weighted as Karis's fit weights an
    // ambient light's reflection, and the roughest mip around the normal stands in for the light
    // the diffuse share scatters.
    if (environment)
    {
        float lastMip = lighting.environment.y;
        float3 reflected = environmentMap.SampleLevel(reflect(-v, n), roughness * lastMip).rgb;
        float3 around = environmentMap.SampleLevel(n, lastMip).rgb;
        color += lighting.environment.x * occlusion * (diffuseColor * around + reflected * ambientReflected(f0, roughness, nv));
    }

    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light light = lighting.lights[i];
        float3 arrived = arriving(light, input.world, n);
        if (int(light.positionAndKind.w) == Ambient)
        {
            color += arrived * occlusion * (diffuseColor + ambientReflected(f0, roughness, nv));
            continue;
        }

        if (i == lighting.shadowLight)
            arrived *= lightReaching(input.world, normalize(input.normal));
        else if (light.cone.z > 0.0 && int(light.positionAndKind.w) == Spot)
            arrived *= spotLightReaching(input.world, normalize(input.normal), light.positionAndKind.xyz, int(light.cone.z) - 1);
        else if (light.cone.z > 0.0)
            arrived *= pointLightReaching(input.world, normalize(input.normal), light.positionAndKind.xyz, int(light.cone.z) - 1);
        float3 l = -fromLight(light, input.world);
        float3 fresnel;
        float3 specular = specularReflected(n, v, l, f0, alpha, fresnel);
        // What the surface reflects at its face is not there to scatter diffusely.
        color += arrived * (diffuseColor * (1.0 - fresnel) + specular);
    }
    return toDisplay(color + emitted(input));
}
