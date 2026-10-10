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
// set from the program, is taken as already encoded, as the 2D passes take every color, in the
// window and in a render texture alike. The window's frame holds light past white, encoded on past
// 1, and one pass after the scene brings it under 1 with the tonemap, so light is bent there once,
// after it is blended. A render texture holds eight bits, so its view bends the light here.
module modelpass;

// sRGB's transfer functions and the tonemap, which a shader importing this module reaches too.
__exported import color;
// The lights, the shadow maps, the probes and the rest of the lights' set, which a shader that
// imports this module reads too, and what reads them, which it does not see.
__exported import lightset;
import lights;
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

// Linear color as the view stores it. The light is scaled by the view's exposure first, 1 for
// every view but a reflection probe's faces, which draw at a quarter and are scaled back as the
// capture is read. The window's frame takes it encoded on past 1, the pass after the scene
// bending it under 1; a render texture's eight bits take it bent and encoded here; and a probe's
// faces keep it linear.
public float3 toDisplay(float3 linear)
{
    float exposure = lighting.environment.w > 0.0 ? lighting.environment.w : 1.0;
    if (lighting.output.x > 0.0)
        return linear * exposure;
    if (lighting.output.y > 0.0)
        return linearToSrgbPastWhite(linear * exposure);
    return linearToSrgb(tonemap(linear * exposure));
}

// The material's factors at this point, which its instance carried through the vertex stage.
public float4 materialColor(ModelVertexOutput input) { return input.color; }
public float materialMetallic(ModelVertexOutput input) { return input.factors.x; }
public float materialRoughness(ModelVertexOutput input) { return input.factors.y; }
public float materialNormalScale(ModelVertexOutput input) { return input.factors.z; }
public float materialOcclusionStrength(ModelVertexOutput input) { return input.factors.w; }
public float3 materialEmission(ModelVertexOutput input) { return input.emission.xyz; }

// The light that bounced to the pixel a fragment is at, irradiance over pi, blended between the
// four screen probes around it that stand on a surface like its own, each by how near it is, how
// alike its normal is to the fragment's, and how near its distance from the eye is to the distance
// the fragment's own plane lies at under the probe's tile middle, found from how the fragment's
// point moves from pixel to pixel, so a floor seen at a slant takes the probes up and down its slope
// as well as those beside it. Or -1 where none does.
public float3 screenBounced(ModelVertexOutput input, float3 n)
{
    uint across, down;
    screenLight.GetDimensions(across, down);
    float2 g = input.position.xy / lighting.screen.x - 0.5;
    int2 base = int2(floor(g));
    float2 f = g - float2(base);
    float distance = length(input.toEye);
    float3 acrossOne = ddx(input.world), downOne = ddy(input.world);
    float4 sum = float4(0.0);
    for (int corner = 0; corner < 4; corner++)
    {
        int2 offset = int2(corner & 1, corner >> 1);
        int2 at = clamp(base + offset, int2(0), int2(int(across) - 1, int(down) - 1));
        float4 light = screenLight.Load(int3(at, 0));
        float4 surface = screenSurfaces.Load(int3(at, 0));
        if (light.a < 0.5 || surface.w < 0.0)
            continue;
        float2 toProbe = (float2(at) + 0.5) * lighting.screen.x - input.position.xy;
        float expected = length(input.toEye - acrossOne * toProbe.x - downOne * toProbe.y);
        float2 lerps = lerp(1.0 - f, f, float2(offset));
        float weight = lerps.x * lerps.y * pow(saturate(dot(n, surface.xyz)), 8.0)
                     * exp(-abs(surface.w - expected) / (0.02 * distance + 0.05));
        sum += float4(light.rgb, 1.0) * weight;
    }
    return sum.w > 1e-3 ? sum.rgb / sum.w / 3.14159265 : float3(-1.0);
}

// The share of the light from all around that reaches the pixel a fragment is at, 1 where the view
// has no occlusion. The map is half the window's size, so a pixel reads it at half its position.
public float screenOcclusion(ModelVertexOutput input)
{
    if (lighting.ambientOcclusion.x <= 0.0)
        return 1.0;
    uint width, height;
    ambientOcclusionMap.GetDimensions(width, height);
    return ambientOcclusionMap.SampleLevel(lightLinear, input.position.xy * 0.5 / float2(width, height), 0).r;
}

// The share of the sun's light its contact shadows through the scene's distance field let reach
// the pixel a fragment is at, 1 where the view has none.
public float sunContact(ModelVertexOutput input)
{
    if (lighting.ambientOcclusion.y <= 0.0)
        return 1.0;
    uint width, height;
    ambientOcclusionMap.GetDimensions(width, height);
    return ambientOcclusionMap.SampleLevel(lightLinear, input.position.xy * 0.5 / float2(width, height), 0).g;
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

// A color as it is, given sRGB-encoded with no tonemap to bend it, or linear for a probe's faces,
// as raylib's default shader draws a model, its texture times its color, so a color near white in
// a render texture is not bent by the curve the lit colors take there.
public float3 unlit(float3 albedo)
{
    if (lighting.output.x > 0.0)
        return albedo;
    return lighting.output.y > 0.0 ? linearToSrgbPastWhite(albedo) : linearToSrgb(albedo);
}

// The light a surface sends toward the eye at a point, linear, in two shares: what it scatters
// diffusely, which subsurface scattering spreads under a surface, and what it reflects, which
// stays where it is. Neither holds the light the material gives off.
public struct LitLight
{
    public float3 diffuse;
    public float3 specular;
}

// A linear color lit at this point by each light reflected by the material's metallic-roughness
// model, the shadowed light cut by its shadow map and the light from all around darkened by its
// occlusion map, with the light that bounced where light bounces, in its two shares.
public LitLight litLight(float3 albedo, ModelVertexOutput input)
{
    LitLight light;
    light.diffuse = float3(0.0);
    light.specular = float3(0.0);
    float3 n = surfaceNormal(input);
    int probe = probeAt(input.world);
    bool environment = lighting.environment.z > 0.0 || probe >= 0;

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
        light.diffuse += occlusion * diffuseColor * bouncedHere;

    // A glossy surface's reflection traced through the scene where light bounces, which takes the
    // place of the probe's and the environment's as far as it met a surface and fades back to
    // them as the surface grows rough.
    float4 traced = tracedReflection(input.world, n, reflect(-v, n), roughness, input.world + input.toEye);
    float gloss = traced.a * (1.0 - smoothstep(lighting.reflection.y * 0.5, lighting.reflection.y, roughness));
    float3 specularShare = ambientReflected(f0, roughness, nv);
    light.specular += occlusion * traced.rgb * gloss * specularShare;

    // The environment, as an ambient light that differs by direction. The mirror direction is
    // looked up at the mip prefiltered for this roughness and weighted as Karis's fit weights an
    // ambient light's reflection, and the diffuse share scatters the irradiance the harmonics hold.
    // Inside a probe's box the probe's capture stands in for the environment.
    if (probe >= 0)
    {
        float lastMip = lighting.probes[probe].halfSizeAndMip.w;
        float3 reflected = probeSample(probe, boxProjected(probe, input.world, reflect(-v, n)), roughness * lastMip);
        float3 around = bounces ? float3(0.0) : probeIrradiance(probe, n);
        float strength = lighting.probes[probe].centerAndIntensity.w * occlusion;
        light.diffuse += strength * diffuseColor * around;
        light.specular += strength * reflected * specularShare * (1.0 - gloss);
    }
    else if (environment)
    {
        float lastMip = lighting.environment.y;
        float3 reflected = environmentMap.SampleLevel(lightLinear, reflect(-v, n), roughness * lastMip).rgb;
        float3 around = bounces ? float3(0.0) : environmentIrradiance(n);
        float strength = lighting.environment.x * occlusion;
        // Where light bounces, by the share of the sky the probes around the surface see along its
        // mirror direction, so a room sealed from the sky reflects none of it, where the occlusion
        // alone left a rough wall in a sealed cave a blue sheen.
        float sky = bounces ? skySeen(input.world, n, reflect(-v, n)) : 1.0;
        light.diffuse += strength * diffuseColor * around;
        light.specular += strength * reflected * specularShare * (1.0 - gloss) * sky;
    }

    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light lamp = lighting.lights[i];
        float3 arrived = arriving(lamp, input.world, n);
        if (int(lamp.positionAndKind.w) == Ambient)
        {
            light.diffuse += arrived * occlusion * (bounces ? float3(0.0) : diffuseColor);
            light.specular += arrived * occlusion * specularShare * (1.0 - gloss);
            continue;
        }

        if (i == lighting.shadowLight)
            arrived *= lightReaching(input.world, normalize(input.normal)) * sunContact(input);
        else if (lamp.cone.z > 0.0 && int(lamp.positionAndKind.w) == Spot)
            arrived *= spotLightReaching(input.world, normalize(input.normal), lamp.positionAndKind.xyz, int(lamp.cone.z) - 1);
        else if (lamp.cone.z > 0.0)
            arrived *= pointLightReaching(input.world, normalize(input.normal), lamp.positionAndKind.xyz, int(lamp.cone.z) - 1);
        float3 l = -fromLight(lamp, input.world);
        float3 fresnel;
        float3 specular = specularReflected(n, v, l, f0, alpha, fresnel);
        // What the surface reflects at its face is not there to scatter diffusely.
        light.diffuse += arrived * diffuseColor * (1.0 - fresnel);
        light.specular += arrived * specular;
    }
    return light;
}

// Whether a point is drawn unlit, as raylib draws a model before a program makes a light: with no
// light, no environment, no probe and no light that bounces.
public bool drawnUnlit(ModelVertexOutput input)
{
    return lighting.count == 0 && lighting.environment.z <= 0.0 && lighting.indirect.x <= 0.0 && probeAt(input.world) < 0;
}

// A linear color lit at this point, given sRGB-encoded for the frame, its two shares of light
// with the light the material gives off, through the tonemap and the encoding. With no light and
// no environment it is the color unlit, as raylib draws a model before a program makes a light,
// with the light the material gives off added.
public float3 lit(float3 albedo, ModelVertexOutput input)
{
    if (drawnUnlit(input))
        return unlit(albedo + emitted(input));
    LitLight light = litLight(albedo, input);
    return toDisplay(light.diffuse + light.specular + emitted(input));
}
