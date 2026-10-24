// Where a point lies in the cascades of the world's probes, which stand in the field's cascades of
// the same number but the far one's (probesOf), and how much of the light from all around them the
// probes give it across each one's outer band (gi.slang, the model pass).
module cascades;

import scenefield;

// How far off a surface along its normal the blend toward the light past the probes is weighed
// from (probesHold), in the probes' spacing. The probes themselves were read from there as well,
// since a room whose walls lie in planes of probes, read at the surface itself, took its walls'
// light from the probes in their planes, which stood inside them and held nothing; such a probe is
// moved into the room before its wall (gi_trace.slang), and the probes are read at the surface
// (bouncedAt), where leaned half a spacing off they were read standing where 61 to 64% more light
// arrives than at the grazing floor's room's far wall, which every bounce took on. The blend keeps
// the lean, since with none a wall in the outer band of Low's last cascade took a closed room's
// absent ambient light, 46% under.
public static const float Lean = 0.5;

// The cells of a cascade's outer band, a probes' spacing, across which the indirect light fades
// from its probes to the next cascade's, and from the last cascade's to the light from beyond it
// (bouncedHold). Read from one cascade alone, the light stepped where each ended, a canyon's floor
// under a sky with no sun by 14, 10 and 15% from one row of pixels to the next at the second's, the
// third's and the last's end at High, and the voxel game's hills at sunset from 0.6 to 1.0 of the
// sky's light at the last's.
public static const float CascadeBand = 8.0;

// How much of the indirect light a point p takes from cascade c rather than the next, all of it
// inside, falling to none across c's outer band (CascadeBand), kept clear of the eye
// (bandHold), past the cell fieldCascade keeps.
public float cascadeHold(SceneFieldInfo info, int c, float3 p)
{
    float4 oc = cascadeAt(info, c);
    return bandHold(info, c, (p - oc.xyz) / oc.w, CascadeBand, 1.0);
}

// How much of the light from all around a surface at p facing n the probes of the cascades up to
// `upTo` give rather than what lies past them, all of it inside the coarsest of them built, falling
// to none across its outer band (CascadeBand), kept clear of the eye (bandHold), and none past it.
// The point is taken leaned off the surface by half a spacing (Lean), so a floor on the box's lower
// side, its probes half a spacing over it, lies half a spacing inside, or as the surface itself
// where that holds more, because a surface facing the eye leans four cells toward the side
// behind it, which may stand 1.6 cells behind the eye, and a pillar's face a block before the eye
// leaned past that side, held nothing, and took the sky's light whole at three headings of a turn
// in the voxel game's cave. Read from cascade `upTo` whether built or not, a closed room's floor in the
// frames before its last cascade was built took the ambient light unhidden in the place of global
// illumination, 9.4 times the light it settles at.
public float probesHold(SceneFieldInfo info, float3 p, float3 n, float spacing, int upTo)
{
    int c = fieldCoarsestBuilt(info, upTo);
    if (c < 0)
        return 0.0;
    float4 oc = cascadeAt(info, c);
    float3 cell = (p - oc.xyz) / oc.w;
    return max(bandHold(info, c, cell + n * (Lean * spacing), CascadeBand, 0.0), bandHold(info, c, cell, CascadeBand, 0.0));
}

// How much of the light from all around a surface at p facing n the probes give rather than what
// lies past them, a sky over a ground (distantAround in lights), through as many cascades as
// `cascades` (probesHold), the probes found where the field's cascades they stand in lie (probesOf).
public float bouncedHold(SceneFieldInfo info, float3 p, float3 n, float spacing, float cascades)
{
    return probesHold(probesOf(info, int(cascades)), p, n, spacing, int(cascades) - 1);
}
