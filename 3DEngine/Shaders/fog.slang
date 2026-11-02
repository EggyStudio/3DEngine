// The volume of fog in front of the window's camera (VolumetricFogRenderer), a cell for each tile
// of sixteen pixels of the window and FogSlices slices of the distance from the eye, each a share
// of the way from start to far that grows with its distance, so the near slices are thin and a
// slice is as deep as it is wide at any distance. fog_scatter.slang lights each cell, and
// fog_integrate.slang sums them from the eye, which decode.slang lays over the scene's light.
module fog;

public static const uint FogSlices = 64;

// Each cell's light and the share of light through it, as fog_scatter.slang and
// fog_integrate.slang read them.
public struct FogUniforms
{
    // The window's clip space to the world, through its camera without temporal antialiasing's move.
    public float4x4 inverseViewProjection;
    // The sun's cascades, each a quarter of its shadow map, as the window's shadow pass drew it this
    // frame.
    public float4x4 sunCascades[4];
    // Each of those cascades' texel, in world units.
    public float4 sunTexels;
    // x: how many of sunCascades hold a cascade. y: a texel of the map, in uv. zw: unused.
    public float4 sunMap;
    // xyz: the eye. w: the distance the slices grow from, where the eye's edge would lie if the
    // first slice grew as the rest do.
    public float4 eye;
    // xyz: toward the sun. w: 1 where there is a sun.
    public float4 toSun;
    // rgb: the sun's light, irradiance. a: how much the fog scatters it forward, from -1 to 1.
    public float4 sunColor;
    // rgb: the light from all around where no cascade of the world's probes holds a cell. a: 1
    // where the world's probes are read.
    public float4 ambient;
    // x: the fog's extinction per unit at and under y, the height it thins above by e every one
    // over z units. w: how far the last slice ends.
    public float4 density;
    // xy: the cells across and down. z: the frame. w: the world's probes along each side of a
    // cascade.
    public float4 grid;
    // x: the world's probes' spacing in cells of the field. y: how many cascades they hold. zw:
    // what a place in the window, from 0 to 1, is multiplied by for its place in the volume, whose
    // tiles reach past the window's edges.
    public float4 probes;
};

// The distance from the eye slice edge k lies at, k from 0, the eye, to FogSlices, the far end.
public float sliceEdge(float k, float start, float far)
{
    return k <= 0.0 ? 0.0 : start * pow(far / start, k / float(FogSlices));
}

// Where distance d lies along the slices, as sliceEdge numbers their edges.
public float sliceAt(float d, float start, float far)
{
    return float(FogSlices) * log(max(d, 1e-6) / start) / log(far / start);
}
