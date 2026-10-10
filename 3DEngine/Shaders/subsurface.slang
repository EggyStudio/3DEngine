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
    // The vertex in clip space, whose place on the picture the scene's depth is read at, the
    // marked images being at half the window's size at the lowest quality.
    float4 clip : TEXCOORD6;
};

struct Marked
{
    float4 diffuse : SV_Target0;
    float4 profile : SV_Target1;
    float4 through : SV_Target2;
};

// How thick the mesh is from a point on its surface facing n toward a light along l, the way the
// light crosses it to reach the point from behind, or -1 where nothing here can say. The scene's
// distance field says it where its finest cascade holds the point and its cells are half the
// reach or less, fine enough to tell the thicknesses the light comes through; where they are
// coarser, a march through cells as wide as skin's reach drew the light in scallops along a
// sphere's terminator, and the sun's shadow map says it instead, the depth from the face the map
// holds nearest the sun to the point.
float thicknessToward(float3 world, float3 n, float3 l, bool sun, float reach)
{
    if (fieldCascade(bouncedField, world, 1.0) == 0 && fieldCell(bouncedField, world) * 2.0 <= reach)
    {
        // The march finds first where it enters the mesh as the field holds it, within a cell of
        // the surface drawn, whose faces the field's cells lie a little off, then where it leaves.
        // Begun at the drawn surface alone, a point where the field's surface lay inside the
        // drawn one leaves at once, and a sphere's shadowed side, a unit through, let the sun's
        // light through in bands. A mesh the field holds nothing of within the cell is thinner
        // than its cells, and lets the light through whole.
        float cell = fieldCell(bouncedField, world);
        float t = 0.0, entered = -1.0;
        float last = 0.0, inside = 0.0;
        for (int step = 0; step < 40 && t - max(entered, 0.0) < reach; step++)
        {
            float d = fieldDistance(SplitVolume(reflectField, lightLinear), bouncedField, world + l * t);
            if (entered < 0.0)
            {
                if (d < 0.0)
                    entered = t;
                else if (t >= cell)
                    return 0.0;
            }
            // Where the march leaves the mesh, between its last step inside and this one, as the
            // distance runs between them, so the thickness changes smoothly across the surface
            // rather than by the march's steps, which the light, falling by e to its power, shows.
            else if (d >= 0.0)
                return last + (t - last) * -inside / (d - inside) - entered;
            if (entered >= 0.0)
            {
                last = t;
                inside = d;
                t += max(-d, cell * 0.25);
            }
            else
                t += cell * 0.25;
        }
        return reach;
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
        // The depths around the point, nine of them a texel apart each blended between the four
        // texels about it, averaged, since one texel's face lies a little off the point's: read
        // texel by texel, they speckled a sphere's shadowed side with the light through and drew
        // the edge of its band along the terminator in the texels' steps.
        float2 texel = 1.0 / float2(width, height);
        float nearest = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                nearest += shadowMap.SampleLevel(lightLinear, clamp(uv + float2(x, y) * texel, (float2(c % 2, c / 2) + texel) * 0.5,
                    (float2(c % 2, c / 2) + 1.0 - texel) * 0.5), 0.0).r;
        nearest /= 9.0;
        // The depth the map's projection gives a unit along the sun's way.
        float perUnit = abs(dot(lighting.shadowCascades[c][2].xyz, l));
        return max(p.z - nearest, 0.0) / max(perUnit, 1e-6);
    }
    return -1.0;
}

// The light that comes through the surface at this point from each light behind it, as the light
// reaching its far side, facing away along -n, falls with the thickness it crosses, each of red,
// green and blue by e to the minus the thickness over its share of the radius, times the surface's
// color. The thickness is measured out to three of the widest share, past which the light is a
// twentieth of what it was and fades on as e has it; cut to nothing there, it ended in a ragged
// edge where the thickness the field or the map gave wavered across that line.
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
        if (thickness < 0.0)
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
    output.clip = output.streams.base.position;
    return output;
}

[shader("fragment")]
Marked fragmentMain(SubsurfaceVertexOutput input)
{
    ModelVertexOutput base = input.streams.base;
    // How far depth changes across a pixel here, worked out before any fragment is dropped.
    float slope = fwidth(base.position.z);
    float scene = sceneDepth.SampleLevel(input.clip.xy / input.clip.w * 0.5 + 0.5, 0).r;
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
