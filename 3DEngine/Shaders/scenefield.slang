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
    // (bandHold). w: unused.
    public float4 eye;
};

// Whether the field holds anything to read.
public bool fieldOn(SceneFieldInfo info)
{
    return info.counts.x > 0.5;
}

// Cascade c's corner and the width of its cell among the field's cascades, or where `probes` is more
// than 0 among those the world's `probes` cascades of probes stand in, the field's own in order but
// for the last, which stands in the far cascade where the field has one, so a cascade of probes lies
// where its cascade does and the probes holding a point are found as the field's cascades holding it
// are (cascadesOf). The probes' reads take the info as the uniform holds it with the count beside
// it, since an info made from it for the probes, its last cascade written over, was copied into
// each thread's own memory to be indexed and read from there, several copies a pixel in the model
// pass, which drew the voxel game's cherry grove at 1920 by 1080 in 66 ms on the development
// machine's GPU, 20 with the cascades written over no more, and 10 with no info made at all.
public float4 cascadeAt(SceneFieldInfo info, int c, int probes = 0)
{
    int far = int(info.counts.w);
    return info.cascades[probes > 0 && far > 0 && c == probes - 1 ? far : c].originAndCell;
}

// How many of the field's cascades a point is looked for in, or of those the world's `probes`
// cascades of probes stand in where `probes` is more than 0 (cascadeAt).
public int cascadesOf(SceneFieldInfo info, int probes = 0)
{
    return probes > 0 ? min(probes, int(info.counts.x)) : int(info.counts.x);
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
public float bandHold(SceneFieldInfo info, int c, float3 cell, float band, float margin, int probes = 0)
{
    float4 oc = cascadeAt(info, c, probes);
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
public int fieldCoarsestBuilt(SceneFieldInfo info, int upTo, int probes = 0)
{
    [loop]
    for (int i = min(upTo, cascadesOf(info, probes) - 1); i >= 0; i--)
        if (cascadeAt(info, i, probes).w > 0.0)
            return i;
    return -1;
}

// The finest built cascade p lies in, at least margin cells inside its sides, or -1 for none.
public int fieldCascade(SceneFieldInfo info, float3 p, float margin, int probes = 0)
{
    int count = cascadesOf(info, probes);
    float size = info.counts.y;
    for (int i = 0; i < count; i++)
    {
        float4 oc = cascadeAt(info, i, probes);
        if (oc.w <= 0.0)
            continue;
        float3 cell = (p - oc.xyz) / oc.w;
        if (all(cell >= margin) && all(cell <= size - margin))
            return i;
    }
    return -1;
}

// How far beyond its offset a double-sided face's plane is kept in a cell, in cells, as
// field_resolve.slang keeps it.
static const float SheetLength = 2.0;

// How far either side of its plane a double-sided face is held below zero, in cells, which leaves
// two sheets two cells apart a quarter of a cell between them and a march's start half a cell off
// one. A march steps a quarter of a cell at least and meets a surface under a tenth, so a plane
// held at no thickness, come at from a little over a tenth, was stepped over to a little over a
// tenth past it; held a tenth either side, a closed room of sheets on the grid took 0.037 of the
// sky's light at High where the half cell held it to 0.016, and a quarter 0.018.
static const float SheetHalf = 0.25;

// The distance from p to the nearest surface in cascade i, in world units, blended between the
// eight cells around it, each read no nearer a side than half a cell, so cascades stacked in the
// texture do not bleed into each other. A double-sided face is held below zero within half a cell
// either side where the cells around keep its plane (sheetAt in field_resolve.slang), as every
// reader but the probes' rays takes the field (fieldDistanceSheetIn), so a march meets a sheet
// wherever it lies between the cells' middles.
public float fieldDistanceIn<V : IVolume>(V field, SceneFieldInfo info, int i, float3 p)
{
    float4 oc = info.cascades[i].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(i) * size) / (size * info.counts.x));
    float4 read = field.sampleAt(uvw, 0);
    return read.r - 0.5 * oc.w * saturate(length(read.yzw));
}

// The distance from p to the nearest surface in cascade i as the probes' rays take it, the cells'
// blend or p's distance from the plane of a double-sided face the cell it lies in keeps, held a
// quarter of a cell either side, where nearer. So a ray meets a sheet thinner than a cell at its
// plane from either side wherever it lies between the cells' middles, where the cells' blend holds
// it no nearer than half a cell, and two sheets two cells apart, as the voxel game's crops stand,
// leave the cells between them open, where held within half a cell either side every cell between
// them read a surface and a screen probe there met its own wall at its rays' start. The probes'
// rays alone take it, since each sample read the plane cost the model pass's march 1.3 ms more in
// the voxel game's cherry grove at High at 1920 by 1080 with no sheet near.
public float fieldDistanceSheetIn<V : IVolume>(V field, SceneFieldInfo info, int i, float3 p)
{
    float4 oc = info.cascades[i].originAndCell;
    float size = info.counts.y;
    float3 cell = clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5);
    float3 uvw = float3(cell.xy / size, (cell.z + float(i) * size) / (size * info.counts.x));
    float4 read = field.sampleAt(uvw, 0);
    float d = read.r;
    // The planes blended with the rest read nothing where no cell around keeps one, so the cell's
    // own is read only near a sheet, in a branch the compiler keeps, since flattened every sample
    // read it and the grove's global illumination took 1.2 ms more.
    [branch]
    if (d < oc.w * 0.75 && dot(read.yzw, read.yzw) >= 1e-6)
    {
        float3 at = min(floor(cell), size - 1.0);
        float3 sheet = field.loadAt(int3(at.xy, at.z + float(i) * size)).yzw;
        float kept = length(sheet);
        if (kept >= 0.5)
            d = min(d, abs(dot(p - oc.xyz - (at + 0.5) * oc.w, sheet) / kept - (kept - SheetLength) * oc.w) - SheetHalf * oc.w);
    }
    return d;
}

// Whether the cell of cascade i that p lies in keeps the plane of a double-sided face (sheetAt in
// field_resolve.slang), with its way and how far p lies from it along that way.
public bool sheetIn<V : IVolume>(V field, SceneFieldInfo info, int i, float3 p, out float3 way, out float off)
{
    float4 oc = info.cascades[i].originAndCell;
    float size = info.counts.y;
    int3 at = int3(min(floor(clamp((p - oc.xyz) / oc.w, 0.5, size - 0.5)), size - 1.0));
    float3 sheet = field.loadAt(int3(at.xy, at.z + i * int(size))).yzw;
    float kept = length(sheet);
    way = kept < 0.5 ? float3(0.0) : sheet / kept;
    off = dot(p - (oc.xyz + (float3(at) + 0.5) * oc.w), way) - (kept - SheetLength) * oc.w;
    return kept >= 0.5;
}

// The distance from p to the nearest surface, from the finest cascade around it, or far where none
// holds it.
public float fieldDistance<V : IVolume>(V field, SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.0);
    return i < 0 ? 1e6 : fieldDistanceIn(field, info, i, p);
}

// The same as the probes' rays take it (fieldDistanceSheetIn).
public float fieldDistanceSheet<V : IVolume>(V field, SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.0);
    return i < 0 ? 1e6 : fieldDistanceSheetIn(field, info, i, p);
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

// The way out from the nearest surface at p as the probes' rays take the field
// (fieldDistanceSheetIn). Where the nearest is a double-sided face whose plane p's cell keeps, it is
// the plane's way toward the side p lies on, its own way on the plane, since across a sheet the
// gradient of its distance cancels, and a world probe standing on a sheet of the voxel game's
// grid, as a sheet on a cell's side does, was moved off it no way in particular, its rays seeing
// both sides, so a closed room of such sheets took a sixth of the sky's light at Low where shifted
// off the grid a two-hundredth.
public float3 fieldNormalSheet<V : IVolume>(V field, SceneFieldInfo info, float3 p)
{
    int i = fieldCascade(info, p, 1.5);
    if (i < 0)
        return float3(0, 1, 0);
    float4 oc = info.cascades[i].originAndCell;
    float d = fieldDistanceSheetIn(field, info, i, p);
    [branch]
    if (d < oc.w * 0.75)
    {
        float3 way;
        float off;
        if (sheetIn(field, info, i, p, way, off) && abs(off) - SheetHalf * oc.w <= d + 1e-4 * oc.w)
            return off >= 0.0 ? way : -way;
    }
    float e = oc.w * 0.5;
    float3 g = float3(
        fieldDistanceSheetIn(field, info, i, p + float3(e, 0, 0)) - fieldDistanceSheetIn(field, info, i, p - float3(e, 0, 0)),
        fieldDistanceSheetIn(field, info, i, p + float3(0, e, 0)) - fieldDistanceSheetIn(field, info, i, p - float3(0, e, 0)),
        fieldDistanceSheetIn(field, info, i, p + float3(0, 0, e)) - fieldDistanceSheetIn(field, info, i, p - float3(0, 0, e)));
    float length2 = dot(g, g);
    return length2 > 1e-12 ? g * rsqrt(length2) : float3(0, 1, 0);
}

// Where a ray from origin along way first comes within a tenth of a cell of a surface between
// tMin and tMax, through the finest cascade that holds each point along it, or -1 where it reaches
// tMax first or leaves every cascade.
public float traceField<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float tMin, float tMax)
{
    float t = tMin;
    for (int i = 0; i < 64 && t < tMax; i++)
    {
        float3 p = origin + way * t;
        int c = fieldCascade(info, p, 1.0);
        if (c < 0)
            return -1.0;
        float cell = info.cascades[c].originAndCell.w;
        float d = fieldDistanceIn(field, info, c, p);
        if (d < cell * 0.1)
            return t;
        t += max(d, cell * 0.25);
    }
    return -1.0;
}

// The same as the probes' rays take the field (fieldDistanceSheetIn).
public float traceFieldSheets<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float tMin, float tMax)
{
    float t = tMin;
    for (int i = 0; i < 64 && t < tMax; i++)
    {
        float3 p = origin + way * t;
        int c = fieldCascade(info, p, 1.0);
        if (c < 0)
            return -1.0;
        float cell = info.cascades[c].originAndCell.w;
        float d = fieldDistanceSheetIn(field, info, c, p);
        if (d < cell * 0.1)
            return t;
        t += max(d, cell * 0.25);
    }
    return -1.0;
}

// As traceFieldSheets, stopping where the ray leaves cascade `most` and every finer one, as -1,
// since a wall thinner than a coarser cascade's cell may let it through.
public float traceFieldWithinSheets<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float tMin, float tMax, int most)
{
    float t = tMin;
    for (int i = 0; i < 64 && t < tMax; i++)
    {
        float3 p = origin + way * t;
        int c = fieldCascade(info, p, 1.0);
        if (c < 0 || c > most)
            return -1.0;
        float cell = info.cascades[c].originAndCell.w;
        float d = fieldDistanceSheetIn(field, info, c, p);
        if (d < cell * 0.1)
            return t;
        t += max(d, cell * 0.25);
    }
    return -1.0;
}

// The alpha a cell of the field's colors is painted with by a surface that lets `through` of the
// light pass a unit of its depth (ModelMaterial.LightThrough), its color painted times it, 1 for a
// solid surface, down to a half for one that lets all of it through, so a reader that divides a
// blend of the colors by their alphas takes the colors back and weighs such a cell less, and one
// that reads a cell as it is takes the share back (lightThroughOf).
public float lightThroughPaint(float through)
{
    return 1.0 - 0.5 * saturate(through);
}

// The share of light a cell painted with alpha lets pass a unit of its surface's depth, 0 for a
// solid surface's cell or one no surface painted.
public float lightThroughOf(float alpha)
{
    return alpha > 0.25 ? saturate(2.0 * (1.0 - alpha)) : 0.0;
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
