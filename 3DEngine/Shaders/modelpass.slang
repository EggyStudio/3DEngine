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
// set from the program, is taken as already encoded, as the 2D passes take every color. With bloom
// on, the window's view leaves its light linear instead, and the frame is tonemapped and encoded
// once bloom has spread it.
module modelpass;

// sRGB's transfer functions and the tonemap, which a shader importing this module reaches too.
__exported import color;
import scenefield;
import gi;

// One drawn copy of a mesh, read from a vertex buffer stepped once per instance, so every mesh
// sharing a mesh and its maps is drawn by one call. A vertex stage of a shader's own takes it as a
// parameter after the mesh's three and hands it to transformModelVertex. It holds nothing of the
// camera, which modelPush carries for the whole call, so an instance is the same through every
// camera and every light.
public struct ModelInstance
{
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

// What a call is drawn through, world to clip space, as System.Numerics lays it out: the camera's
// in the model pass, and the light's in the shadow pass.
public struct ModelPush
{
    public float4x4 viewProjection;
};

[[vk::push_constant]]
public ConstantBuffer<ModelPush> modelPush;

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

// Linear color as the frame stores it, tonemapped first, since a target holds sRGB-encoded bytes.
// The light is scaled by the view's exposure first, 1 for every view but a reflection probe's
// faces, which draw at a quarter, so light up to four times the tonemap's knee survives their eight
// bits and is scaled back as the capture is read. A view drawn into the HDR frame, which bloom
// spreads and a later pass tonemaps, keeps the light as it is.
public float3 toDisplay(float3 linear)
{
    float exposure = lighting.environment.w > 0.0 ? lighting.environment.w : 1.0;
    if (lighting.output.x > 0.0)
        return linear * exposure;
    return linearToSrgb(tonemap(linear * exposure));
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

// One reflection probe, as ProbeUboEntry holds it: the middle of its box, where it was captured
// from, and its intensity, and half the box's size and its cube's last mip. Its irradiance is in
// the buffer of its slot, which the GPU writes as it filters the capture.
public struct Probe
{
    public float4 centerAndIntensity;
    public float4 halfSizeAndMip;
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
    // The environment map's intensity, its last mip, 1 when there is one, and the exposure a
    // reflection probe's faces are drawn at, 0 meaning 1.
    public float4 environment;
    public Light lights[16];
    // The width of a point light's shadow texel per unit of distance from it, in x, how many are
    // shadowed in y, and in z how many of them have a layer to each face.
    public float4 pointShadow;
    // World space to each face's clip space for each shadowed point light, six to a light in the
    // order +X, -X, +Y, -Y, +Z, -Z.
    public float4x4 pointShadowFaces[72];
    // The width of each shadowed spot light's texel per unit of distance from it, by slot.
    public float4 spotShadowTexels[3];
    // World space to each shadowed spot light's clip space, by slot.
    public float4x4 spotShadows[10];
    // How many probes are bound, in x, and the probes by the slot their cube is bound at.
    public float4 probeCount;
    public Probe probes[4];
    // 1 in x when the view is drawn into the HDR frame, whose light toDisplay leaves linear.
    public float4 output;
    // 1 in x when the view's ambient light is darkened by ambientOcclusionMap, the window's alone,
    // and 1 in y when the shadowed directional light is darkened by its green channel, the sun's
    // contact shadows through the scene's distance field.
    public float4 ambientOcclusion;
    // 1 in x when the view's light from all around is the light that bounced, read from
    // bouncedLight, the window's alone. y: the probes' spacing in cells of the field. z: the probes
    // along each side of a cascade. w: how many cascades.
    public float4 indirect;
    // x: the pixels along each side of a screen probe's tile. y: 1 when the screen's probes, in
    // screenLight and screenSurfaces, are read before the world's.
    public float4 screen;
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

// The bound reflection probes' cubes, prefiltered as the environment map is, or black cubes.
[[vk::binding(5, 1)]]
public SamplerCube probeMap0;
[[vk::binding(6, 1)]]
public SamplerCube probeMap1;
[[vk::binding(7, 1)]]
public SamplerCube probeMap2;
[[vk::binding(8, 1)]]
public SamplerCube probeMap3;

// How much of the light from all around reaches each pixel of the window, at half its size, from
// ao.slang, or a white texture where no occlusion is worked out.
[[vk::binding(9, 1)]]
public Sampler2D ambientOcclusionMap;

// The bound reflection probes' irradiance over pi, nine spherical harmonic coefficients in xyz
// each, which the filter that made each probe's cube wrote, or zeros.
[[vk::binding(10, 1)]]
public StructuredBuffer<float4> probeIrradiance0;
[[vk::binding(11, 1)]]
public StructuredBuffer<float4> probeIrradiance1;
[[vk::binding(12, 1)]]
public StructuredBuffer<float4> probeIrradiance2;
[[vk::binding(13, 1)]]
public StructuredBuffer<float4> probeIrradiance3;

// The environment's irradiance the same way, or zeros where there is none.
[[vk::binding(14, 1)]]
public StructuredBuffer<float4> environmentIrradianceBuffer;

// The light that bounced to each probe, six faces a probe, every cascade, as gi_ambient.slang
// gathers it, and where the scene's distance field it was traced through lies, which the probes
// lie in, or a field of nothing where light does not bounce.
[[vk::binding(15, 1)]]
public Sampler3D bouncedLight;

[[vk::binding(16, 1)]]
public ConstantBuffer<SceneFieldInfo> bouncedField;

// The light arriving at each of the screen's probes, irradiance, its alpha 1 where the probe holds
// it, and each probe's surface's normal and distance from the eye, a texel a probe of the tiles
// the window is cut into, as gi_screen.slang leaves them, or zeros where light does not bounce.
[[vk::binding(17, 1)]]
public Sampler2D screenLight;

[[vk::binding(18, 1)]]
public Sampler2D screenSurfaces;

// The light that bounced to the pixel a fragment is at, irradiance over pi, blended between the
// four screen probes around it that stand on a surface like its own, each by how near it is and
// how alike its normal and its distance from the eye are to the fragment's, or -1 where none does.
public float3 screenBounced(ModelVertexOutput input, float3 n)
{
    uint across, down;
    screenLight.GetDimensions(across, down);
    float2 g = input.position.xy / lighting.screen.x - 0.5;
    int2 base = int2(floor(g));
    float2 f = g - float2(base);
    float distance = length(input.toEye);
    float4 sum = float4(0.0);
    for (int corner = 0; corner < 4; corner++)
    {
        int2 offset = int2(corner & 1, corner >> 1);
        int2 at = clamp(base + offset, int2(0), int2(int(across) - 1, int(down) - 1));
        float4 light = screenLight.Load(int3(at, 0));
        float4 surface = screenSurfaces.Load(int3(at, 0));
        if (light.a < 0.5 || surface.w < 0.0)
            continue;
        float2 lerps = lerp(1.0 - f, f, float2(offset));
        float weight = lerps.x * lerps.y * pow(saturate(dot(n, surface.xyz)), 8.0)
                     * exp(-abs(surface.w - distance) / (0.02 * distance + 0.05));
        sum += float4(light.rgb, 1.0) * weight;
    }
    return sum.w > 1e-3 ? sum.rgb / sum.w / 3.14159265 : float3(-1.0);
}

// The light that bounced to a surface at p facing n, irradiance over pi, from the world's probes
// around it as gi.slang blends them, or -1 where no cascade of probes holds p.
public float3 bounced(float3 p, float3 n)
{
    int c = fieldCascade(bouncedField, p, 1.0);
    if (c < 0 || c >= int(lighting.indirect.w))
        return float3(-1.0);
    return bouncedAt(bouncedLight, bouncedField, p, n, lighting.indirect.z, lighting.indirect.y, lighting.indirect.w) / 3.14159265;
}

// The share of the light from all around that reaches the pixel a fragment is at, 1 where the view
// has no occlusion. The map is half the window's size, so a pixel reads it at half its position.
public float screenOcclusion(ModelVertexOutput input)
{
    if (lighting.ambientOcclusion.x <= 0.0)
        return 1.0;
    uint width, height;
    ambientOcclusionMap.GetDimensions(width, height);
    return ambientOcclusionMap.SampleLevel(input.position.xy * 0.5 / float2(width, height), 0).r;
}

// The share of the sun's light its contact shadows through the scene's distance field let reach
// the pixel a fragment is at, 1 where the view has none.
public float sunContact(ModelVertexOutput input)
{
    if (lighting.ambientOcclusion.y <= 0.0)
        return 1.0;
    uint width, height;
    ambientOcclusionMap.GetDimensions(width, height);
    return ambientOcclusionMap.SampleLevel(input.position.xy * 0.5 / float2(width, height), 0).g;
}

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

// What a mesh with a color or a second texture coordinate at each vertex gives the fragment stage
// beside what every mesh does: the color in linear color, white where the mesh has none, and the
// coordinate, zero where it has none. model_streams.slang draws with it, and a shader of the
// program's own reads it by taking this in place of ModelVertexOutput, whose fields are in base.
// A mesh without either is drawn without it, so its vertices carry no more to the fragment stage.
public struct ModelStreamsOutput
{
    public ModelVertexOutput base;
    public float4 vertexColor : COLOR3;
    public float2 uv2 : TEXCOORD3;
};

// Where the camera is, worked out from its view-projection alone, so the call carries no more
// than it. The transform here is as mul takes it, its rows the columns System.Numerics has. Under
// a perspective projection the eye is the one point clip space's x, y and w all take as zero,
// three planes whose meeting point Cramer's rule gives. An orthographic projection has no such
// point, and gives the way toward the camera instead, with w of 0.
public float4 eyeInWorld(float4x4 transform)
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

// The vertex stage for a mesh's color and second texture coordinate as well, the color in sRGB as a
// mesh gives it.
public ModelStreamsOutput transformModelVertex(float3 position, float3 normal, float2 uv, float4 color, float2 uv2, ModelInstance instance)
{
    ModelStreamsOutput output;
    output.base = transformModelVertex(position, normal, uv, instance);
    output.vertexColor = float4(srgbToLinear(color.rgb), color.a);
    output.uv2 = uv2;
    return output;
}

// The vertex stage every model shader uses unless it writes its own.
public ModelVertexOutput transformModelVertex(float3 position, float3 normal, float2 uv, ModelInstance instance)
{
    // Read by columns, as a constant buffer's matrix is, so the rows System.Numerics writes are the
    // columns of what mul takes, as the shadow stage reads it.
    float4x4 viewProjection = modelPush.viewProjection;
    ModelVertexOutput output;
    output.world = float3(dot(instance.worldX, float4(position, 1.0)), dot(instance.worldY, float4(position, 1.0)), dot(instance.worldZ, float4(position, 1.0)));
    output.position = mul(viewProjection, float4(output.world, 1.0));
    output.normal = float3(dot(instance.worldX.xyz, normal), dot(instance.worldY.xyz, normal), dot(instance.worldZ.xyz, normal));
    output.uv = uv;

    float4 eye = eyeInWorld(viewProjection);
    output.toEye = eye.w == 0.0 ? eye.xyz : eye.xyz - output.world;
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

// The same times the mesh's color at this point.
public float4 baseColor(ModelStreamsOutput input)
{
    return baseColor(input.base) * input.vertexColor;
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
    float offset = lighting.spotShadowTexels[slot / 4][slot % 4] * distance(world, lightPosition) * 1.5;
    float4 clip = mul(lighting.spotShadows[slot], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (any(abs(p.xy) > 0.995) || p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height;
    shadowMap.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    // The last tile is the map's lower right quarter, split by rank as ShadowFit.SpotTileArea
    // splits it: whole for one light, quarters for up to four, and past four quarters for the
    // first two and sixteenths in its lower half for the rest.
    int count = lighting.spotShadowCount;
    float size = 0.5;
    float2 origin = float2(0.5, 0.5);
    if (count > 4 && slot >= 2)
    {
        size = 0.125;
        origin = float2(0.5, 0.75) + float2((slot - 2) % 4, (slot - 2) / 4) * 0.125;
    }
    else if (count > 1)
    {
        size = 0.25;
        origin = float2(0.5, 0.5) + float2(slot % 2, slot / 2) * 0.25;
    }
    // Kept a texel inside the square, so the comparisons around an edge read no other light's.
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * size, origin + texel, origin + size - texel);
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
    // The lights that matter most have a layer to each face, and the rest a quarter of one at half
    // the size, as ShadowFit.PointFaceArea lays them out, with texels twice as wide.
    int full = int(lighting.pointShadow.z);
    int layer = slot * 6 + face;
    float2 origin = float2(0.0, 0.0);
    float scale = 1.0;
    if (slot >= full)
    {
        int index = (slot - full) * 6 + face;
        layer = full * 6 + index / 4;
        origin = float2(index % 4 % 2, index % 4 / 2) * 0.5;
        scale = 0.5;
    }
    float offset = lighting.pointShadow.x / scale * length(away) * 1.5;
    float4 clip = mul(lighting.pointShadowFaces[slot * 6 + face], float4(world + n * offset, 1.0));
    if (clip.w <= 0.0)
        return 1.0;
    float3 p = clip.xyz / clip.w;
    if (p.z > 1.0 || p.z < 0.0)
        return 1.0;

    uint width, height, layers;
    pointShadowMap.GetDimensions(width, height, layers);
    float2 texel = 1.0 / float2(width, height);
    float2 uv = clamp(origin + (p.xy * 0.5 + 0.5) * scale, origin + texel, origin + scale - texel);
    float reached = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            reached += p.z - 0.00002 <= pointShadowMap.SampleLevel(float3(uv + float2(x, y) * texel, layer), 0.0).r ? 1.0 : 0.0;
    return reached / 9.0;
}

// The way light comes from one light to a point, for specular.
float3 fromLight(Light light, float3 world)
{
    int kind = int(light.positionAndKind.w);
    return kind == Directional ? light.directionAndRange.xyz : normalize(world - light.positionAndKind.xyz);
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

// The light a white diffuse surface facing n returns from nine spherical harmonic coefficients in
// EnvironmentMap.Irradiance's order.
float3 harmonics(float4 c[9], float3 n)
{
    float3 sum = c[0].xyz * 0.282095
        + c[1].xyz * (0.488603 * n.y)
        + c[2].xyz * (0.488603 * n.z)
        + c[3].xyz * (0.488603 * n.x)
        + c[4].xyz * (1.092548 * n.x * n.y)
        + c[5].xyz * (1.092548 * n.y * n.z)
        + c[6].xyz * (0.315392 * (3.0 * n.z * n.z - 1.0))
        + c[7].xyz * (1.092548 * n.x * n.z)
        + c[8].xyz * (0.546274 * (n.x * n.x - n.y * n.y));
    return max(sum, float3(0.0));
}

// A bound probe's irradiance from the buffer of its slot.
float3 probeIrradiance(int i, float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
    {
        if (i == 0) c[k] = probeIrradiance0[k];
        else if (i == 1) c[k] = probeIrradiance1[k];
        else if (i == 2) c[k] = probeIrradiance2[k];
        else c[k] = probeIrradiance3[k];
    }
    return harmonics(c, n);
}

// The bound probe whose box holds a point, the smallest where boxes overlap, or -1. A point a
// hundredth past a wall of the box still counts, so a room's wall built on the box's wall is in it
// everywhere rather than in it and out of it pixel by pixel.
int probeAt(float3 p)
{
    int found = -1;
    float smallest = 1e30;
    for (int i = 0; i < min(int(lighting.probeCount.x), 4); i++)
    {
        float3 h = lighting.probes[i].halfSizeAndMip.xyz;
        float3 d = abs(p - lighting.probes[i].centerAndIntensity.xyz);
        float volume = h.x * h.y * h.z;
        if (all(d <= h * 1.01 + 0.01) && volume < smallest)
        {
            found = i;
            smallest = volume;
        }
    }
    return found;
}

// A probe's cube along a direction at a mip, by its slot.
float3 probeSample(int i, float3 direction, float mip)
{
    if (i == 0) return probeMap0.SampleLevel(direction, mip).rgb;
    if (i == 1) return probeMap1.SampleLevel(direction, mip).rgb;
    if (i == 2) return probeMap2.SampleLevel(direction, mip).rgb;
    return probeMap3.SampleLevel(direction, mip).rgb;
}

// The way into a probe's cube that a ray from p along r meets the box's wall, from the middle the
// probe was captured at, so a reflection lands where the room's wall is rather than at infinity.
float3 boxProjected(int i, float3 p, float3 r)
{
    float3 center = lighting.probes[i].centerAndIntensity.xyz;
    float3 h = lighting.probes[i].halfSizeAndMip.xyz;
    float3 far = (center + h - p) / r;
    float3 near = (center - h - p) / r;
    float3 exits = max(far, near);
    float t = min(exits.x, min(exits.y, exits.z));
    return p + r * t - center;
}

// The light a white diffuse surface facing n returns from the environment.
float3 environmentIrradiance(float3 n)
{
    float4 c[9];
    for (int k = 0; k < 9; k++)
        c[k] = environmentIrradianceBuffer[k];
    return harmonics(c, n);
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

// A color as it is, given sRGB-encoded for the frame with no tonemap to bend it, or linear for
// the HDR frame, as raylib's default shader draws a model, its texture times its color, so a
// color near white is not bent by the curve the lit colors take.
public float3 unlit(float3 albedo)
{
    return lighting.output.x > 0.0 ? albedo : linearToSrgb(albedo);
}

// A linear color lit at this point, given sRGB-encoded for the frame. With light entities it is
// each light reflected by the material's metallic-roughness model, the shadowed light cut by its
// shadow map and the light from all around darkened by its occlusion map, adding the light the
// material gives off, through the tonemap and the encoding. With no light and no environment it
// is the color unlit, as raylib draws a model before a program makes a light, with the light the
// material gives off added.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    float3 n = surfaceNormal(input);
    int probe = probeAt(input.world);
    bool environment = lighting.environment.z > 0.0 || probe >= 0;
    if (lighting.count == 0 && !environment && lighting.indirect.x <= 0.0)
        return unlit(albedo + emitted(input));

    float4 packed = metallicRoughnessMap.Sample(input.uv);
    float metallic = saturate(materialMetallic(input) * packed.b);
    float roughness = clamp(materialRoughness(input) * packed.g, 0.045, 1.0);
    // Occlusion darkens the light from all around, which reaches into a crease less, and leaves
    // the direct light, which the shadow map takes care of.
    float occlusion = lerp(1.0, occlusionMap.Sample(input.uv).r, saturate(materialOcclusionStrength(input))) * screenOcclusion(input);
    float alpha = roughness * roughness;
    float3 f0 = lerp(float3(0.04), albedo, metallic);
    float3 diffuseColor = albedo * (1.0 - metallic);

    float3 v = normalize(input.toEye);
    float nv = max(dot(n, v), 1e-4);
    float3 color = float3(0.0);

    // The light that bounced, where light bounces and a cascade of probes holds the point, which
    // stands in for the diffuse share of the light from all around, the environment's, a probe's
    // and the ambient lights', its rays having met that light where they met nothing.
    float3 bouncedHere = float3(-1.0);
    if (lighting.indirect.x > 0.0)
    {
        if (lighting.screen.y > 0.0)
            bouncedHere = screenBounced(input, normalize(input.normal));
        if (bouncedHere.x < 0.0)
            bouncedHere = bounced(input.world, n);
    }
    bool bounces = bouncedHere.x >= 0.0;
    if (bounces)
        color += occlusion * diffuseColor * bouncedHere;

    // The environment, as an ambient light that differs by direction. The mirror direction is
    // looked up at the mip prefiltered for this roughness and weighted as Karis's fit weights an
    // ambient light's reflection, and the diffuse share scatters the irradiance the harmonics hold.
    // Inside a probe's box the probe's capture stands in for the environment.
    if (probe >= 0)
    {
        float lastMip = lighting.probes[probe].halfSizeAndMip.w;
        float3 reflected = probeSample(probe, boxProjected(probe, input.world, reflect(-v, n)), roughness * lastMip);
        float3 around = bounces ? float3(0.0) : probeIrradiance(probe, n);
        color += lighting.probes[probe].centerAndIntensity.w * occlusion * (diffuseColor * around + reflected * ambientReflected(f0, roughness, nv));
    }
    else if (environment)
    {
        float lastMip = lighting.environment.y;
        float3 reflected = environmentMap.SampleLevel(reflect(-v, n), roughness * lastMip).rgb;
        float3 around = bounces ? float3(0.0) : environmentIrradiance(n);
        color += lighting.environment.x * occlusion * (diffuseColor * around + reflected * ambientReflected(f0, roughness, nv));
    }

    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light light = lighting.lights[i];
        float3 arrived = arriving(light, input.world, n);
        if (int(light.positionAndKind.w) == Ambient)
        {
            color += arrived * occlusion * ((bounces ? float3(0.0) : diffuseColor) + ambientReflected(f0, roughness, nv));
            continue;
        }

        if (i == lighting.shadowLight)
            arrived *= lightReaching(input.world, normalize(input.normal)) * sunContact(input);
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
