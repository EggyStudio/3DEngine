// How global illumination's rays cross the cells of a surface that lets a share of light pass a
// unit of its depth (ModelMaterial.LightThrough), as a canopy's leaves, where they would stop at a
// solid surface's. The share rides in the alpha of the field's colors (lightThroughPaint in
// scenefield.slang).
module lightthrough;

import scenefield;
import gi;

// The share of light the surface met at p lets pass a unit of its depth, read from the cell p lies
// in as it is, 0 for a solid surface's cell or one no surface painted.
public float lightThroughAt<V : IVolume>(V albedo, SceneFieldInfo info, float3 p)
{
    int c = fieldCascade(info, p, 1.0);
    if (c < 0)
        return 0.0;
    float4 oc = info.cascades[c].originAndCell;
    int size = int(info.counts.y);
    int3 cell = clamp(int3(floor((p - oc.xyz) / oc.w)), int3(0), int3(size - 1));
    return lightThroughOf(albedo.loadAt(int3(cell.xy, cell.z + c * size)).a);
}

// How far a ray along `way` from `origin` that met such a surface at t runs before it has left it,
// half a cell a step while the field holds the point within half a cell of a surface or inside one,
// which the march goes on from, and in inside whether it is still within at t plus `most`, its
// interval's end, or after 64 steps, as a ray grazing a canopy is, where it ends that far in. A
// canopy's leaves, closed blocks the field holds inside, are crossed whole, and a leaf drawn as one
// sheet, which the field holds half a cell either side, is a cell or so deep.
public float lightThroughDepth<V : IVolume>(V field, SceneFieldInfo info, float3 origin, float3 way, float t, float most, out bool inside)
{
    float depth = 0.0;
    inside = true;
    [loop]
    for (int step = 0; step < 64; step++)
    {
        float3 p = origin + way * (t + depth);
        float cell = fieldCell(info, p);
        if (cell <= 0.0 || depth > 0.0 && fieldDistance(field, info, p) > 0.5 * cell)
        {
            inside = false;
            return depth;
        }
        depth += 0.5 * cell;
        if (depth >= most)
            return most;
    }
    return depth;
}

// The share of light a ray keeps crossing a surface that lets `passing` through a unit of its
// depth, run through for `depth` from a hit with cells `cell` wide, less the three quarters of a cell
// the hit lies before the surface and the march's last step leaves past it, where it left it.
public float lightThroughKept(float passing, float depth, float cell, bool inside)
{
    return pow(passing, inside ? depth : max(depth - 0.75 * cell, 0.0));
}

// Whether nothing the field holds stands between a and b but surfaces that let light through, as
// seesThrough in gi.slang asks with clearance kept clear at either end, the light a ray crossing
// them keeps being its own to take. The merge asks it of the probes of the cascade above, which a
// canopy hid from those under it, so the floor under one 4 units up took no sky through the screen's
// probes however much of it the leaves let through.
public bool seesPastLeaves<V : IVolume, A : IVolume>(V field, A albedo, SceneFieldInfo info, float3 a, float3 b, float clearance)
{
    float3 toward = b - a;
    float length = length(toward);
    if (length <= clearance * 2.0)
        return true;
    float3 way = toward / length;
    float t = clearance;
    [loop]
    for (int layer = 0; layer < 4; layer++)
    {
        t = traceField(field, info, a, way, t, length - clearance);
        if (t < 0.0)
            return true;
        if (lightThroughAt(albedo, info, a + way * t) <= 0.0)
            return false;
        bool inside = false;
        t += lightThroughDepth(field, info, a, way, t, length - clearance - t, inside);
        if (inside)
            return true;
    }
    return false;
}
