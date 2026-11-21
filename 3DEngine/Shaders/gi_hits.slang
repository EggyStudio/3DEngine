// The surfaces the probes' rays meet as gi_trace.slang traces them and gi_merge.slang marches them
// on, the field's images and the lights they are lit by at the trace's binding numbers, which both
// kernels import, and the field and the sky as the RadianceCascades module's passes read them
// (IProbeField, ISky), shaded as the trace shades a hit.
module gi_hits;

import gi;
import scenefield;
import probelight;
import lightthrough;

[[vk::binding(0, 0)]]
public Sampler3D sceneField;

[[vk::binding(1, 0)]]
public ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(2, 0)]]
public Sampler3D fieldAlbedo;

[[vk::binding(3, 0)]]
public Sampler3D fieldGlow;

[[vk::binding(4, 0)]]
public ConstantBuffer<GiLights> lights;

// The indirect light reaching each probe the frame before, as gi_ambient.slang left it, for the
// light a surface sends on from it.
[[vk::binding(6, 0)]]
public Sampler3D bounced;

// The light from all around, for a ray of the last cascade that meets nothing, or a black cube.
[[vk::binding(7, 0)]]
public SamplerCube environment;

// The way each cell's nearest face looks, which a hit takes its color from cells by.
[[vk::binding(11, 0)]] [format("r32ui")]
public Texture3D<uint> fieldFaces;

// The second plane each cell of the field keeps where two double-sided faces lie as near it, which
// the rays read with the field (SheetVolume).
[[vk::binding(12, 0)]]
public Texture3D<float4> fieldSecond;

// The window's shadow map as its shadow pass drew it the frame before, which a hit reads the sun
// through where the lights' cascades of it hold the hit, or a white texture where they hold none.
[[vk::binding(13, 0)]]
public Sampler2D sunShadow;

// The faces' light at the surfaces they are read for, which a hit's probes read (gi_ambient.slang).
[[vk::binding(15, 0)]]
public Sampler3D bouncedAtSurface;

// The most steps a ray of a cascade below the last takes through the field, past which it is handed
// on from where it stopped, the last cascade's rays running to their end, since past it no cascade
// gives what lies beyond.
public static const uint StepCap = 64u;

// The field's layout with each cascade at the corner it lay at the frame before, where the faces of
// the indirect light, which the rays read, were gathered. Read at this frame's corners, a
// cascade that moves with the eye reads each probe's faces from the probe a step or more beside
// it, and the light past the first bounce, fed from those faces each frame, takes many frames to
// find itself again after a turn moves every cascade.
public SceneFieldInfo gatheredAt(int cascades)
{
    SceneFieldInfo then = fieldInfo;
    int far = int(fieldInfo.counts.w);
    // Written where cascadeAt reads each cascade of probes from, the last at the far cascade's
    // place where the field has one.
    for (int c = 0; c < min(cascades, MostProbeCascades); c++)
        then.cascades[far > 0 && c == cascades - 1 ? far : c].originAndCell.xyz = lights.before[c].xyz;
    // The cells a shift brought in this frame lie at this frame's corner.
    then.waitingFirst.w = 0.0;
    return then;
}

// The sky a probe's ray takes where it leaves the field, as the module's passes read it (ISky),
// the environment along the way times the sky's light where an environment is set, or the sky's
// light alone.
public struct ShadedSky : ISky
{
    public float3 skyAlong(float3 way)
    {
        return lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
    }
}

// The light the surface a ray stopped at gives of its own, and the field's normal there, which
// hitLight leaves for the thread that called it, the stop's call being a ray's last.
public static float3 hitOwn;
public static float3 hitNormalAt;

// The field a probe's rays are traced through, as the module's passes read it (IProbeField), its
// distances as the field's reads give them from the bound table of cascades, the light a surface a
// ray meets sends back as this trace shades it, with the probe's share and the texel's widening,
// and the share of light a surface lets through as its paint's alpha says, none in the build for a
// frame where no surface lets light through.
public struct ShadedField : IProbeField
{
    public float3 share;
    public float widen;
    // The probes along each side, their spacing in cells and how many cascades there are.
    public float probes;
    public float spacing;
    public float cascades;

    public int cascadeOf(float3 p, float margin) { return fieldCascade(fieldInfo, p, margin); }
    public float4 cornerAndCell(int cascade) { return fieldInfo.cascades[cascade].originAndCell; }
    public int size() { return int(fieldInfo.counts.y); }

    public float4 between(int cascade, float3 cells)
    {
        float size = fieldInfo.counts.y;
        return sceneField.sampleAt(float3(cells.xy / size, (cells.z + float(cascade) * size) / (size * fieldInfo.counts.x)), 0);
    }

    public float4 cellAt(int cascade, int3 cell) { return sceneField.loadAt(int3(cell.xy, cell.z + cascade * int(fieldInfo.counts.y))); }

    public float3 secondAt(int cascade, int3 cell)
    {
        return SheetVolume(sceneField, fieldSecond).secondAt(int3(cell.xy, cell.z + cascade * int(fieldInfo.counts.y)));
    }

    // The cascade and the cell go unread, the shading finding the hit's own.
    public float3 hitLight(float3 place, float3 way, int cascade, int3 cell)
    {
        return shadeProbeHitN(sceneField, SheetVolume(sceneField, fieldSecond), fieldInfo, gatheredAt(int(cascades)), fieldAlbedo, fieldFaces, fieldGlow, lights, bounced, bouncedAtSurface, bounced, place,
            way, probes, spacing, cascades, true, share, SunShadowMap(sunShadow), hitOwn, hitNormalAt, false, widen);
    }

    public float throughAt(int cascade, int3 cell)
    {
#if defined(NO_LIGHT_THROUGH)
        return 0.0;
#else
        return lightThroughOf(fieldAlbedo.loadAt(int3(cell.xy, cell.z + cascade * int(fieldInfo.counts.y))).a);
#endif
    }
}
