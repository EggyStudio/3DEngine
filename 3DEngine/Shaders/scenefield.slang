// The scene's distance field as the passes that read it see it. Cascades of cells around the
// camera, each twice as coarse and as wide as the one before, and after them the far cascade where
// the program asks for one, its cells as wide as its reach takes, hold how far each cell's middle is
// from the nearest surface of the meshes that cast shadows, below zero inside a mesh with an inside,
// exact within a few cells of a surface and held at that many beyond. They lie one after another
// along z in one texture, so a pass binds the field once, and a cascade not yet built has a cell
// size of 0 and is passed over.
module scenefield;

// A 3D image a pass reads the field, its colors or the indirect light through, sampled between
// texels at a level or a texel loaded as it is. A pass that binds its images with their samplers
// passes them as they are, and the model pass, which binds its images apart from a few samplers
// they share, since a stage may have no more than sixteen samplers on Metal, passes a SplitVolume.
public interface IVolume
{
    public float4 sampleAt(float3 uvw, float lod);
    public float4 loadAt(int3 texel);
}

public extension Sampler3D : IVolume
{
    public float4 sampleAt(float3 uvw, float lod) { return this.SampleLevel(uvw, lod); }
    public float4 loadAt(int3 texel) { return this.Load(int4(texel, 0)); }
}

// An image and the sampler it is read through, bound apart.
public struct SplitVolume : IVolume
{
    public Texture3D image;
    public SamplerState sampler;

    public __init(Texture3D image, SamplerState sampler)
    {
        this.image = image;
        this.sampler = sampler;
    }

    public float4 sampleAt(float3 uvw, float lod) { return image.SampleLevel(sampler, uvw, lod); }
    public float4 loadAt(int3 texel) { return image.Load(int4(texel, 0)); }
}

public struct FieldCascade
{
    // xyz: the world position of the cascade's corner, its first cell's least corner. w: the
    // width of a cell, or 0 for a cascade not yet built.
    public float4 originAndCell;
};

public struct SceneFieldInfo
{
    // x: how many cascades the texture holds, 0 where the field is off. y: the cells along each
    // side of a cascade. z: how many cells from a surface the distances are exact. w: the far
    // cascade's place, the last, or 0 where there is none.
    public float4 counts;
    public FieldCascade cascades[9];
    // xyz: the eye the cascades lie around this frame, which their outer bands keep clear of
    // (bandHold). w: in probesOf's info, the far cascade's place, where its last cascade is read
    // from (cascadeAt), or 0.
    public float4 eye;
};

// Whether the field holds anything to read.
public bool fieldOn(SceneFieldInfo info)
{
    return info.counts.x > 0.5;
}

// Cascade c's corner and the width of its cell, or in probesOf's info the far cascade's for its last
// cascade where the field has one.
public float4 cascadeAt(SceneFieldInfo info, int c)
{
    int far = int(info.eye.w);
    return info.cascades[far > 0 && c == int(info.counts.x) - 1 ? far : c].originAndCell;
}

// The field's cascades the world's `probes` cascades of probes stand in, the field's own in order
// but for the last, which stands in the far cascade where the field has one, so a cascade of probes
// lies where its cascade does and the probes holding a point are found as the field's cascades
// holding it are. The probes are read through it and the field's distances through the field's own
// info, since past the cascades of probes it holds neither the field's count nor its places in the
// texture. The far cascade's place is kept for cascadeAt to read the last from, the cascades left
// as they are, since a copy of them with the last written over, at a place known only as the shader
// ran, was kept in each thread's own memory and read from there, and the model pass, which takes
// several such copies a pixel, drew the voxel game's cherry grove at 1920 by 1080 in 66 ms on the
// development machine's GPU, and in 20 with the cascades read in place.
public SceneFieldInfo probesOf(SceneFieldInfo info, int probes)
{
    SceneFieldInfo probed = info;
    int far = int(info.counts.w);
    probed.eye.w = far > 0 && probes > 0 && probes <= int(info.counts.x) ? float(far) : 0.0;
    probed.counts.x = float(min(probes, int(info.counts.x)));
    probed.counts.w = 0.0;
    return probed;
}

// The cells of the field's last cascade's outer band, across which a pass that reads the field's
// distances fades to what it gives where the field holds nothing (fieldHold).
public static const float FieldBand = 8.0;

// The cells an outer band ends short of the eye on a side the eye stands near (bandHold), the half
// of a probes' spacing a surface facing the eye is leaned toward it by as the probes are read for
// it, four cells, and two more.
public static const float EyeClear = 6.0;

// How far into cascade c's outer band a point `cell` lies, in its cells, as a share of the band, 1
// at the band's inner edge and beyond and 0 at the side, `margin` cells off each side. A cascade
// lies 0.35 of its width ahead of the eye, so the eye stands 1.6 to 9.6 cells from the side behind
// it, inside a band of eight; on a side the eye is that near the band ends EyeClear cells short of
// it, or is none and the light steps at the side, behind the eye where no surface it sees lies.
// With the last cascade's band across that side, a closed room's walls took the sky's light whole
// wherever the band reached them, by the heading as the cascades turned with the eye, up to 0.14 of
// the open floor's light at half the headings, and with the band kept clear of the eye under 0.0004
// at every one (CascadeEdgeTests).
public float bandHold(SceneFieldInfo info, int c, float3 cell, float band, float margin)
{
    float4 oc = cascadeAt(info, c);
    float3 eye = (info.eye.xyz - oc.xyz) / oc.w;
    float3 low = clamp(eye - margin - EyeClear, 0.0, band);
    float3 high = clamp(info.counts.y - eye - margin - EyeClear, 0.0, band);
    float3 held = saturate(min((cell - margin) / max(low, 1e-3), (info.counts.y - cell - margin) / max(high, 1e-3)));
    return min(min(held.x, held.y), held.z);
}

// How much of what the field holds at p a pass takes, all of it inside the field's cascades,
// falling to none across the outer band (FieldBand) of the coarsest one built, kept clear of the
// eye (bandHold), and none past it, so a pass that reads the field to its edge, as the occlusion
// from it, the sun's contact shadows or a glow light's march, fades there rather than stepping.
// Taken from the last cascade whether built or not, it read none while the last was not built, and
// a turn of five pixels moved the floor's light across an emissive block's penumbra by 16% where 3%
// (GlowPenumbraTests).
public float fieldHold(SceneFieldInfo info, float3 p)
{
    int c = fieldCoarsestBuilt(info, int(info.counts.x) - 1);
    if (c < 0)
        return 0.0;
    float4 oc = cascadeAt(info, c);
    return bandHold(info, c, (p - oc.xyz) / oc.w, FieldBand, 1.0);
}

// The coarsest built cascade no coarser than `upTo`, or -1 where none is built, as in an app's
// first frames, where the field builds its cascades finest first as many a frame as its budget
// allows.
public int fieldCoarsestBuilt(SceneFieldInfo info, int upTo)
{
    [loop]
    for (int i = min(upTo, int(info.counts.x) - 1); i >= 0; i--)
        if (cascadeAt(info, i).w > 0.0)
            return i;
    return -1;
}

// The finest built cascade p lies in, at least margin cells inside its sides, or -1 for none.
public int fieldCascade(SceneFieldInfo info, float3 p, float margin)
{
    int count = int(info.counts.x);
    float size = info.counts.y;
    for (int i = 0; i < count; i++)
    {
        float4 oc = cascadeAt(info, i);
        if (oc.w <= 0.0)
            continue;
        float3 cell = (p - oc.xyz) / oc.w;
        if (all(cell >= margin) && all(cell <= size - margin))
            return i;
    }
    return -1;
}

// The distance from p to the nearest surface in cascade i, in world units, blended between the
// eight cells around it, each read no nearer a side than half a cell, so cascades stacked in the
// texture do not bleed into each other.
public float fieldDistanceIn<V : IVolume>(V field, SceneFieldInfo info, int i, float3 p)
{
    float4 oc = info.cascades[i].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(i) * size) / (size * info.counts.x));
    return field.sampleAt(uvw, 0).r;
}

// The distance from p to the nearest surface, from the finest cascade around it, or far where none
// holds it.
public float fieldDistance<V : IVolume>(V field, SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.0);
    return i < 0 ? 1e6 : fieldDistanceIn(field, info, i, p);
}

// The way out from the nearest surface at p, the field's gradient across half a cell, or up where
// the field is flat or holds nothing there.
public float3 fieldNormal<V : IVolume>(V field, SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.5);
    if (i < 0)
        return float3(0, 1, 0);
    float e = info.cascades[i].originAndCell.w * 0.5;
    float3 g = float3(
        fieldDistanceIn(field, info, i, p + float3(e, 0, 0)) - fieldDistanceIn(field, info, i, p - float3(e, 0, 0)),
        fieldDistanceIn(field, info, i, p + float3(0, e, 0)) - fieldDistanceIn(field, info, i, p - float3(0, e, 0)),
        fieldDistanceIn(field, info, i, p + float3(0, 0, e)) - fieldDistanceIn(field, info, i, p - float3(0, 0, e)));
    float length2 = dot(g, g);
    return length2 > 1e-12 ? g * rsqrt(length2) : float3(0, 1, 0);
}

// The width of a cell of the finest built cascade around p, or 0 where none holds it.
public float fieldCell(SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.0);
    return i < 0 ? 0.0 : cascadeAt(info, i).w;
}

// The way a face looks in eight bits, four along each side of the octahedron the sphere is folded
// onto, as field_splat.slang keeps it in each cell's uint, enough to tell faces that look apart
// from faces that meet square; and the way back from the eight bits.
public uint faceBits(float3 n)
{
    n /= abs(n.x) + abs(n.y) + abs(n.z);
    float2 o = n.z >= 0.0 ? n.xy : (1.0 - abs(n.yx)) * float2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    uint2 q = min(uint2((o * 0.5 + 0.5) * 16.0), uint2(15u));
    return q.x | (q.y << 4);
}

public float3 faceWay(uint bits)
{
    float2 o = (float2(float(bits & 15u), float((bits >> 4) & 15u)) + 0.5) / 16.0 * 2.0 - 1.0;
    float3 n = float3(o, 1.0 - abs(o.x) - abs(o.y));
    if (n.z < 0.0)
        n.xy = (1.0 - abs(n.yx)) * float2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    return normalize(n);
}
