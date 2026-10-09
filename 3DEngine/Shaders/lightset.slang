// The model pass's lights' descriptor set: the lights and the shadow maps that cut them, the
// environment map and the reflection probes, the occlusion, and where light bounces the probes it
// bounced into and what a glossy surface traces its reflection through. Imported by lights, which
// reads it, and passed on by modelpass to every shader that imports modelpass.
module lightset;

import scenefield;
import gi;

// One light, packed by LightingUboPacker: position and kind, direction and range, color times
// intensity, and the cosines of a spot's inner and outer angles.
public struct Light
{
    public float4 positionAndKind;
    public float4 directionAndRange;
    public float4 colorAndShadow;
    public float4 cone;
};

// One reflection probe, as ProbeUboEntry holds it: the middle of its box, where it was captured
// from, and its intensity, and half the box's size and its cube's last mip. Its irradiance is in
// the buffer of its slot, which the GPU writes as it filters the capture.
public struct Probe
{
    public float4 centerAndIntensity;
    public float4 halfSizeAndMip;
};

// Laid out as LightingUbo is. The count, the shadowed light's index or -1, how many cascades it
// has and the shadowed spot light's index or -1 come first, then the world width of a texel of each cascade, the cascades'
// transforms nearest first, and the lights.
public struct Lighting
{
    public int count;
    public int shadowLight;
    public int cascadeCount;
    // How many spot lights cast shadows, drawn into the map's last tile, the whole of it for one
    // and a quarter each for more.
    public int spotShadowCount;
    // A texel's world width for each cascade, and in w the spot tile's per unit of distance.
    public float4 shadowTexels;
    public float4x4 shadowCascades[4];
    // The environment map's intensity, its last mip, 1 when there is one, and the exposure a
    // reflection probe's faces are drawn at, 0 meaning 1.
    public float4 environment;
    public Light lights[16];
    // The width of a point light's shadow texel per unit of distance from it, in x, how many are
    // shadowed in y, and in z how many of them have a layer to each face.
    public float4 pointShadow;
    // World space to each face's clip space for each shadowed point light, six to a light in the
    // order +X, -X, +Y, -Y, +Z, -Z.
    public float4x4 pointShadowFaces[72];
    // The width of each shadowed spot light's texel per unit of distance from it, by slot.
    public float4 spotShadowTexels[3];
    // World space to each shadowed spot light's clip space, by slot.
    public float4x4 spotShadows[10];
    // How many probes are bound, in x, and the probes by the slot their cube is bound at.
    public float4 probeCount;
    public Probe probes[4];
    // 1 in x when toDisplay leaves the view's light linear, a reflection probe's faces, and 1 in y
    // when it encodes it on past 1 with no tonemap, the window's frame, which a pass after the
    // scene bends. Neither for a render texture, whose light toDisplay bends and encodes.
    public float4 output;
    // 1 in x when the view's ambient light is darkened by ambientOcclusionMap, the window's or a
    // render texture's drawn through a camera, never a probe's faces, and 1 in y when the shadowed
    // directional light is darkened by its green channel, the sun's contact shadows through the
    // scene's distance field.
    public float4 ambientOcclusion;
    // 1 in x when the view's light from all around is the light that bounced, read from
    // bouncedLight, in every view where light bounces. y: the probes' spacing in cells of the
    // field. z: the probes along each side of a cascade. w: how many cascades.
    public float4 indirect;
    // x: the pixels along each side of a screen probe's tile. y: 1 when the screen's probes, in
    // screenLight and screenSurfaces, are read before the world's.
    public float4 screen;
    // The window's camera this frame, world to clip space and back, and the frame before's, both
    // ways, which a glossy surface's reflection is traced through the window's depth with and
    // looked up in the frame before's picture by.
    public float4x4 reflectViewProjection;
    public float4x4 reflectInverseViewProjection;
    public float4x4 reflectLastViewProjection;
    public float4x4 reflectLastInverseViewProjection;
    // x: 1 when a glossy surface traces its reflection, the window's alone. y: the roughness from
    // which none is traced, the probes and the environment reflecting alone. z: the steps through
    // the window's depth. w: 1 when reflectHistory holds the frame before.
    public float4 reflection;
    // x: how far a reflection is traced through the window's depth. y: 1 when a reflection the
    // field misses is traced through rayScene, where the device traces rays. zw: unused.
    public float4 reflectionReach;
};

[[vk::binding(0, 1)]]
public ConstantBuffer<Lighting> lighting;

// The depths the shadowed light sees, or a white texture, which reads as nothing in shadow.
[[vk::binding(1, 1)]]
public Texture2D shadowMap;

// The light from all around, prefiltered by roughness down its mips, or a black cube for none.
[[vk::binding(2, 1)]]
public TextureCube environmentMap;

// The same light unfiltered, at about its image's resolution, which sky.slang draws as a backdrop,
// or a black cube for none.
[[vk::binding(3, 1)]]
public TextureCube skyMap;

// The depths each shadowed point light sees, a layer for each of its six faces.
[[vk::binding(4, 1)]]
public Texture2DArray pointShadowMap;

// The bound reflection probes' cubes, prefiltered as the environment map is, or black cubes.
[[vk::binding(5, 1)]]
public TextureCube probeMap0;
[[vk::binding(6, 1)]]
public TextureCube probeMap1;
[[vk::binding(7, 1)]]
public TextureCube probeMap2;
[[vk::binding(8, 1)]]
public TextureCube probeMap3;

// How much of the light from all around reaches each pixel of the view, the window or a render
// texture, at half its size, from ao.slang, or a white texture where no occlusion is worked out.
[[vk::binding(9, 1)]]
public Texture2D ambientOcclusionMap;

// The bound reflection probes' irradiance over pi, nine spherical harmonic coefficients in xyz
// each, which the filter that made each probe's cube wrote, or zeros.
[[vk::binding(10, 1)]]
public StructuredBuffer<float4> probeIrradiance0;
[[vk::binding(11, 1)]]
public StructuredBuffer<float4> probeIrradiance1;
[[vk::binding(12, 1)]]
public StructuredBuffer<float4> probeIrradiance2;
[[vk::binding(13, 1)]]
public StructuredBuffer<float4> probeIrradiance3;

// The environment's irradiance the same way, or zeros where there is none.
[[vk::binding(14, 1)]]
public StructuredBuffer<float4> environmentIrradianceBuffer;

// The light that bounced to each probe, six faces a probe, every cascade, as gi_ambient.slang
// gathers it, and where the scene's distance field it was traced through lies, which the probes
// lie in, or a field of nothing where light does not bounce.
[[vk::binding(15, 1)]]
public Texture3D bouncedLight;

[[vk::binding(16, 1)]]
public ConstantBuffer<SceneFieldInfo> bouncedField;

// The light arriving at each of the screen's probes, irradiance, its alpha 1 where the probe holds
// it, and each probe's surface's normal and distance from the eye, a texel a probe of the tiles
// the window is cut into, as gi_screen.slang leaves them, or zeros where light does not bounce.
[[vk::binding(17, 1)]]
public Texture2D screenLight;

[[vk::binding(18, 1)]]
public Texture2D screenSurfaces;

// The scene's distance field, its surfaces' colors and the light they give off, and the lights
// that light them, which a reflection is traced through past the window's depth and shaded from,
// or a field of nothing where light does not bounce.
[[vk::binding(19, 1)]]
public Texture3D reflectField;

[[vk::binding(20, 1)]]
public Texture3D reflectAlbedo;

[[vk::binding(21, 1)]]
public Texture3D reflectGlow;

[[vk::binding(22, 1)]]
public ConstantBuffer<GiLights> reflectLights;

// The window's depth at half its size this frame, which a reflection is traced through first.
[[vk::binding(23, 1)]]
public Texture2D reflectDepth;

// The window's scene as the frame before drew it, in linear light at half its size with its mips,
// which a reflection that meets a surface on the screen reads that surface's light from.
[[vk::binding(24, 1)]]
public Texture2D reflectHistory;

// The window's depth at half its size the frame before, beside its picture, which says whether the
// picture showed the surface a reflection met there or something in front of it.
[[vk::binding(25, 1)]]
public Texture2D reflectHistoryDepth;

// The two samplers every image of the set is read through, one blending between texels and
// levels and one reading the nearest texel, bound apart from the images, since a stage may have
// no more than sixteen samplers on Metal and the set's images alone are seventeen.
[[vk::binding(26, 1)]]
public SamplerState lightLinear;

[[vk::binding(27, 1)]]
public SamplerState lightNearest;

#if defined(RAY_QUERY)
// Where the device traces rays: the window's meshes as its ray tracing sees them, each drawn copy's
// color, light given off and where its mesh's corners lie, and every mesh's corners in its own
// space, which a reflection the scene's distance field misses is traced and shaded through.
[[vk::binding(28, 1)]]
public RaytracingAccelerationStructure rayScene;

public struct RaySurface
{
    // rgb: the copy's color, linear.
    public float4 color;
    // rgb: the light it gives off, linear.
    public float4 emission;
    // x: its mesh's first corner in rayCorners. yzw: unused.
    public uint4 corners;
};

[[vk::binding(29, 1)]]
public StructuredBuffer<RaySurface> raySurfaces;

[[vk::binding(30, 1)]]
public StructuredBuffer<float4> rayCorners;
#endif
