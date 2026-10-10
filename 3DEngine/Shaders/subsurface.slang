// The window's meshes whose material scatters light under its surface, drawn again after the scene
// into three images of the window's size, one sample a pixel: the light each pixel scatters
// diffusely, as the model pass worked it out, with 1 in alpha, the material's profile, the share
// of its radius each of red, green and blue travels under the surface, and the radius, and the
// light that comes through the surface from a light behind it.
// subsurface_blur.slang then spreads that light over the frame. A fragment is drawn only where the
// scene's depth shows its surface, so a marked surface behind another, a cheek behind a hand or
// a shape drawn in front, leaves its pixels unmarked, and a pixel the scene shows of an unmarked
// surface is never touched. The mesh's colors and second texture coordinates are read as the
// model pass reads them, from the mesh or the one default for every vertex.
import modelpass;
import lights;
import scenefield;

// The scene's depth as its pass left it, its first sample's where the window has several.
[[vk::binding(0, 2)]]
Sampler2D sceneDepth;

struct SubsurfaceVertexOutput
{
    ModelStreamsOutput streams;
    // The material's profile, the same for every instance of the call.
    nointerpolation float4 profile : TEXCOORD5;
};

struct Marked
{
    float4 diffuse : SV_Target0;
    float4 profile : SV_Target1;
    float4 through : SV_Target2;
};

// How thick the mesh is from a point on its surface facing n toward a light along l, the way the
// light crosses it to reach the point from behind, or -1 where nothing here can say. The scene's
// distance field says it where its finest cascade holds the point, a march from a little under the
// surface toward the light until it leaves the mesh, out to reach; past that cascade the sun's
// shadow map says it, the depth from the face the map holds nearest the sun to the point.
float thicknessToward(float3 world, float3 n, float3 l, bool sun, float reach)
{
    if (fieldCascade(bouncedField, world, 1.0) == 0)
    {
        float cell = fieldCell(bouncedField, world);
        float t = cell * 0.25;
        for (int step = 0; step < 32 && t < reach; step++)
        {
            float d = fieldDistance(SplitVolume(reflectField, lightLinear), bouncedField, world + l * t);
            if (d >= 0.0)
                return t;
            t += max(-d, cell * 0.25);
        }
        return t;
    }
    if (!sun)
        return -1.0;
    uint width, height;
    shadowMap.GetDimensions(width, height);
    for (int c = 0; c < lighting.cascadeCount; c++)
    {
        float4 clip = mul(lighting.shadowCascades[c], float4(world, 1.0));
        float3 p = clip.xyz / clip.w;
        if (any(abs(p.xy) > 0.995) || p.z > 1.0)
            continue;
        float2 uv = (p.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
        float nearest = shadowMap.SampleLevel(lightNearest, uv, 0.0).r;
        // The depth the map's projection gives a unit along the sun's way.
        float perUnit = abs(dot(lighting.shadowCascades[c][2].xyz, l));
        return max(p.z - nearest, 0.0) / max(perUnit, 1e-6);
    }
    return -1.0;
}

// The light that comes through the surface at this point from each light behind it, as the light
// reaching its far side, facing away along -n, falls with the thickness it crosses, each of red,
// green and blue by e to the minus the thickness over its share of the radius, times the surface's
// color. A light the thickness past three of the widest share leaves nothing.
float3 lightThrough(ModelVertexOutput base, float3 albedo, float4 profile)
{
    float3 n = normalize(base.normal);
    float3 travel = max(profile.a * saturate(profile.rgb), float3(1e-4));
    float reach = 3.0 * max(travel.r, max(travel.g, travel.b));
    float3 sum = float3(0.0);
    for (int i = 0; i < min(lighting.count, 16); i++)
    {
        Light lamp = lighting.lights[i];
        int kind = int(lamp.positionAndKind.w);
        if (kind == Ambient)
            continue;
        float3 arrived = arriving(lamp, base.world, -n);
        if (all(arrived <= float3(0.0)))
            continue;
        float thickness = thicknessToward(base.world, n, -fromLight(lamp, base.world), i == lighting.shadowLight && kind == Directional, reach);
        if (thickness < 0.0 || thickness >= reach)
            continue;
        sum += arrived * exp(-thickness / travel);
    }
    return sum * albedo;
}

[shader("vertex")]
SubsurfaceVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance,
    [[vk::location(9)]] float4 color : COLOR0, [[vk::location(10)]] float2 uv2 : TEXCOORD1, [[vk::location(11)]] float4 profile : TEXCOORD2)
{
    SubsurfaceVertexOutput output;
    output.streams = transformModelVertex(position, normal, uv, color, uv2, instance);
    output.profile = profile;
    return output;
}

[shader("fragment")]
Marked fragmentMain(SubsurfaceVertexOutput input)
{
    ModelVertexOutput base = input.streams.base;
    // How far depth changes across a pixel here, worked out before any fragment is dropped.
    float slope = fwidth(base.position.z);
    uint width, height;
    sceneDepth.GetDimensions(width, height);
    float scene = sceneDepth.SampleLevel(base.position.xy / float2(width, height), 0).r;
    float4 color = alphaTested(baseColor(input.streams), base);

    Marked output;
    output.diffuse = float4(0.0);
    output.profile = float4(0.0);
    output.through = float4(0.0);
    // Unlit, as raylib draws a model before a program makes a light, there is no light to scatter.
    if (!drawnUnlit(base))
    {
        LitLight light = litLight(color.rgb, base);
        float exposure = lighting.environment.w > 0.0 ? lighting.environment.w : 1.0;
        output.diffuse = float4(light.diffuse * exposure, 1.0);
        output.profile = input.profile;
        output.through = float4(lightThrough(base, color.rgb, input.profile) * exposure, 0.0);
    }
    // The scene's depth is its first sample's, a little off the pixel's middle, so the surface the
    // scene shows lies within two pixels' change of the fragment's, and one nearer hides it.
    if (scene < base.position.z - 2.0 * slope - 1e-6)
        discard;
    return output;
}
