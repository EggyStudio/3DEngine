// The scene's distance field as the passes that read it see it. Cascades of cells around the
// camera, each twice as coarse and as wide as the one before, hold how far each cell's middle is
// from the nearest surface of the meshes that cast shadows, below zero inside a mesh with an inside,
// exact within a few cells of a surface and held at that many beyond. They lie one after another
// along z in one texture, so a pass binds the field once, and a cascade not yet built has a cell
// size of 0 and is passed over.
module scenefield;

// A 3D image a pass reads the field, its colors or the bounced light through: sampled between
// texels at a level, or a texel loaded as it is. A pass that binds its images with their samplers
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
    // side of a cascade. z: how many cells from a surface the distances are exact. w: unused.
    public float4 counts;
    public FieldCascade cascades[8];
};

// Whether the field holds anything to read.
public bool fieldOn(SceneFieldInfo info)
{
    return info.counts.x > 0.5;
}

// The finest built cascade p lies in, at least margin cells inside its sides, or -1 for none.
public int fieldCascade(SceneFieldInfo info, float3 p, float margin)
{
    int count = int(info.counts.x);
    float size = info.counts.y;
    for (int i = 0; i < count; i++)
    {
        float4 oc = info.cascades[i].originAndCell;
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
    return i < 0 ? 0.0 : info.cascades[i].originAndCell.w;
}
