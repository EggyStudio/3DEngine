// The light each cell of the fog's volume scatters toward the eye, a thread a cell. The cell's
// middle is lit by the sun where its shadow map, as the window's shadow pass drew it this frame,
// shows the sun there, scattered toward the eye by Henyey and Greenstein's phase, and by the light
// from all around, the world's probes' irradiance up and down averaged, scattered alike every way.
// Written as the light times the fog's extinction there, beside that extinction, for
// fog_integrate.slang to sum from the eye.
import fog;
import scenefield;
import gi;
import probelight;

[[vk::binding(0, 0)]] [format("rgba16f")]
RWTexture3D<float4> scattered;

// The sun's shadow map, read texel for texel.
[[vk::binding(1, 0)]]
Sampler2D sunShadow;

// The world's probes as the model pass reads them (bounced in lights.slang).
[[vk::binding(2, 0)]]
Sampler3D probeLight;

[[vk::binding(3, 0)]]
Sampler3D probeSky;

[[vk::binding(4, 0)]]
Sampler3D probeReach;

[[vk::binding(5, 0)]]
ConstantBuffer<SceneFieldInfo> field;

[[vk::binding(6, 0)]]
ConstantBuffer<FogUniforms> fog;

// The share of the sun that reaches p, from the first of the sun's cascades that holds it, its
// depth and the eight around it compared as the probes' hits compare them (SunShadowMap in
// gi.slang), or all of it past the cascades.
float sunReaches(float3 p)
{
    int count = int(fog.sunMap.x);
    float texel = fog.sunMap.y;
    for (int c = 0; c < count; c++)
    {
        float4 clip = mul(fog.sunCascades[c], float4(p, 1.0));
        float3 q = clip.xyz / clip.w;
        if (any(abs(q.xy) > 0.995) || q.z > 1.0 || q.z < 0.0)
            continue;
        float2 uv = (q.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
        float shown = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                shown += q.z - 0.0005 <= sunShadow.SampleLevel(uv + float2(x, y) * texel, 0.0).r ? 1.0 : 0.0;
        return shown / 9.0;
    }
    return 1.0;
}

// Henyey and Greenstein's phase for light turned by an angle of cosine cosAngle, forward where g
// is above 0.
float phase(float g, float cosAngle)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(max(1.0 + g2 - 2.0 * g * cosAngle, 1e-4), 1.5));
}

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(fog.grid.x) || id.y >= uint(fog.grid.y) || id.z >= FogSlices)
        return;
    float start = fog.eye.w, far = fog.density.w;
    float2 uv = (float2(id.xy) + 0.5) / fog.grid.xy / fog.probes.zw;
    float4 along = mul(fog.inverseViewProjection, float4(uv * 2.0 - 1.0, 0.5, 1.0));
    float3 way = normalize(along.xyz / along.w - fog.eye.xyz);
    float d = 0.5 * (sliceEdge(float(id.z), start, far) + sliceEdge(float(id.z) + 1.0, start, far));
    float3 p = fog.eye.xyz + way * d;
    float extinction = fog.density.x * exp(-max(p.y - fog.density.y, 0.0) * fog.density.z);
    if (!(extinction > 0.0))
    {
        scattered[id] = float4(0.0);
        return;
    }

    float3 light = float3(0.0);
    if (fog.toSun.w > 0.5)
        light += fog.sunColor.rgb * sunReaches(p) * phase(fog.sunColor.a, dot(way, fog.toSun.xyz));
    // The light from all around scattered alike every way is its mean over the sphere, the
    // irradiance up and down over two pi.
    float3 around = fog.ambient.rgb;
    if (fog.ambient.a > 0.5)
    {
        int c = fieldCascade(field, p, 1.0, int(fog.probes.y));
        if (c >= 0 && c < int(fog.probes.y))
            around = (bouncedAt(probeLight, probeSky, probeReach, field, p, float3(0.0, 1.0, 0.0), fog.grid.w, fog.probes.x, fog.probes.y)
                + bouncedAt(probeLight, probeSky, probeReach, field, p, float3(0.0, -1.0, 0.0), fog.grid.w, fog.probes.x, fog.probes.y))
                / (2.0 * 3.14159265);
    }
    scattered[id] = float4((light + around) * extinction, extinction);
}
