// Where a point lies in the cascades of the world's probes, which stand in the field's cascades of
// the same number but the far one's (cascadeAt), and how much of the light from all around them the
// probes give it across each one's outer band (gi.slang, the model pass).
module cascades;

import scenefield;

// How far off a surface along its normal the blend toward the light past the probes is weighed from
// (probesHold), in the probes' spacing. The probes themselves are read at the surface (bouncedAt),
// since a probe in the plane of a room's wall, which would stand inside it and hold nothing, is
// moved into the room before the wall (gi_trace.slang), and leaned half a spacing off they would be
// read standing where more light arrives than at the surface, as at a room's far wall. The blend
// keeps the lean, since with none a wall in the outer band of Low's last cascade takes the ambient
// light a closed room holds out.
public static const float Lean = 0.5;

// The cells of a cascade's outer band, a probes' spacing, across which the indirect light fades
// from its probes to the next cascade's, and from the last cascade's to the light from beyond it
// (bouncedHold). Read from one cascade alone, the light would step from one row of pixels to the
// next where each ends.
public static const float CascadeBand = 8.0;

// Whether cascade c of the world's `probes` fades into the next across its outer band, where the
// next is built and is not the far cascade's. The last counted cascade holds its light to its side,
// since the far cascade's probes stand in cells that hold the ground near them as a slab, and faded
// into them the pixels inside the counted reach would take that slab's light.
public bool fadesOn(SceneFieldInfo info, int c, int probes)
{
    int next = c + 1;
    return next < probes && !(info.counts.w > 0.5 && next == probes - 1) && cascadeAt(info, next, probes).w > 0.0;
}

// How much of the indirect light a point p takes from cascade c rather than the next, all of it
// inside, falling to none across c's outer band (CascadeBand), kept clear of the eye
// (bandHold), past the cell fieldCascade keeps.
public float cascadeHold(SceneFieldInfo info, int c, float3 p, int probes = 0)
{
    float4 oc = cascadeAt(info, c, probes);
    return bandHold(info, c, (p - oc.xyz) / oc.w, CascadeBand, 1.0, probes);
}

// How much of the light from all around a surface at p facing n the probes of the cascades up to
// `upTo` give rather than what lies past them, all of it inside the coarsest of them built, falling
// to none across its outer band (CascadeBand), kept clear of the eye (bandHold), and none past it.
// The point is taken leaned off the surface by half a spacing (Lean), so a floor on the box's lower
// side, its probes half a spacing over it, lies half a spacing inside, or as the surface itself
// where that holds more, because a surface facing the eye leans four cells toward the side behind
// it, which may stand 1.6 cells behind the eye, so a pillar's face a block before the eye would
// lean past that side, hold nothing and take the sky's light whole. Read from cascade `upTo`
// whether built or not, a closed room's floor would take the ambient light unhidden in the place
// of global illumination in the frames before its last cascade is built.
public float probesHold(SceneFieldInfo info, float3 p, float3 n, float spacing, int upTo, int probes = 0)
{
    int c = fieldCoarsestBuilt(info, upTo, probes);
    if (c < 0)
        return 0.0;
    float4 oc = cascadeAt(info, c, probes);
    float3 cell = (p - oc.xyz) / oc.w;
    if (info.counts.w > 0.5 && probes > 0 && c == probes - 1)
        return max(farHold(info, oc, cell + n * (Lean * spacing)), farHold(info, oc, cell));
    return max(bandHold(info, c, cell + n * (Lean * spacing), CascadeBand, 0.0, probes), bandHold(info, c, cell, CascadeBand, 0.0, probes));
}

// How much of the light from all around a point `cell` the far cascade's probes give rather than
// what lies past them, in its cells, across a band of CascadeBand cells whose outer side lies half
// a snap (SnapCells) inside the cascade from the eye, which the cascade's middle stands no further
// from. Weighed in the cascade's grid, as the cascades below are, the band would move with the
// cascade's corner by a snap at once, as wide as the band, so a cloud over a walk would take the
// probes' light in one frame and the light past them in the next. Weighed around the eye, the band follows a walk
// as smoothly as the eye moves. A cell's width at the cascade's sides holds it to the cascade as it
// is built, for the frames a cascade waits to follow an eye gone past its snap.
public float farHold(SceneFieldInfo info, float4 oc, float3 cell)
{
    float3 eye = (info.eye.xyz - oc.xyz) / oc.w;
    float3 around = saturate((info.counts.y * 0.5 - SnapCells * 0.5 - abs(cell - eye)) / CascadeBand);
    float3 inside = saturate(min(cell, info.counts.y - cell));
    return min(min(around.x, around.y), around.z) * min(min(inside.x, inside.y), inside.z);
}

// How much of the light from all around a surface at p facing n the probes give rather than what
// lies past them, a sky over a ground (distantAround in lights), through as many cascades as
// `cascades` (probesHold), the probes found where the field's cascades they stand in lie (cascadeAt).
public float bouncedHold(SceneFieldInfo info, float3 p, float3 n, float spacing, float cascades)
{
    return probesHold(info, p, n, spacing, int(cascades) - 1, int(cascades));
}
