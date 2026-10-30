// The window's view and a camera's depth, and the march of a ray through a depth image, which the
// screen's probes and the model pass's reflections share, apart from the rest of global
// illumination (gi.slang), which carries it on to every pass that imports that.
module screenmarch;

// A 2D image a ray is traced through as a depth, as IVolume is for the field's images.
public interface IPicture
{
    public float4 sampleAt(float2 uv, float lod);
    // Its texels across and down.
    public float2 size();
}

public extension Sampler2D : IPicture
{
    public float4 sampleAt(float2 uv, float lod) { return this.SampleLevel(uv, lod); }
    public float2 size()
    {
        uint width, height;
        this.GetDimensions(width, height);
        return float2(width, height);
    }
}

// An image and the sampler it is read through, bound apart.
public struct SplitPicture : IPicture
{
    public Texture2D image;
    public SamplerState sampler;

    public __init(Texture2D image, SamplerState sampler)
    {
        this.image = image;
        this.sampler = sampler;
    }

    public float4 sampleAt(float2 uv, float lod) { return image.SampleLevel(sampler, uv, lod); }
    public float2 size()
    {
        uint width, height;
        image.GetDimensions(width, height);
        return float2(width, height);
    }
}

// The view the screen's probes are placed through, which gi_screen.slang traces them through and
// gi_screen_filter.slang blends them through.
public struct ScreenView
{
    // World to clip space, and back, as the window's camera has it this frame.
    public float4x4 viewProjection;
    public float4x4 inverseViewProjection;
    // xyz: the eye.
    public float4 eye;
    // x: a tile's pixels along each side. y: the probes across. z: the probes down. w: 1 where the
    // frame before's light is left out, plus 2 where each probe keeps its own light, its neighbors'
    // left out.
    public float4 grid;
    // x: where the first interval ends. y: the world's probes along each side. z: their spacing in
    // cells. w: how many cascades the world's probes have.
    public float4 trace;
    // xy: the depth's texels across and down. zw: the window's pixels across and down.
    public float4 sizes;
    // World to clip space as the frame before had it, which a probe's place then is found through.
    public float4x4 lastViewProjection;
    // xyz: the frame before's eye. w: 1 where the probes of the frame before may be read, as they
    // may not be on the first frame or the first after the probes were laid out again.
    public float4 last;
    // xy: how far from its tile's middle each probe marches to the glow lights from this frame, in
    // pixels, the same for every probe and moved from frame to frame so a probe's points cover its
    // tile. z: how far along the sixteen places in their texels the probes' rays leave from this
    // frame, moved on by one each frame. w: none.
    public float4 jitter;
    // x: how many places of glow lights there are. y, z: the places whose light is the one the
    // frame before had there, a bit a place, the first 32 in y, as uint bits. w: the field's
    // cascades built this frame as meshes came or went within them or for the first time, a bit a
    // cascade, as uint bits.
    public float4 glows;
    // xyz: the corners of the box around the meshes that came or went within those cascades.
    public float4 changedMin;
    public float4 changedMax;
};

// A camera's matrices and eye, which a ray is traced through its depth with.
public struct ScreenCamera
{
    // World to clip space, and back.
    public float4x4 viewProjection;
    public float4x4 inverseViewProjection;
    public float3 eye;
};

// The world position a point of a depth image at uv and depth d stands for.
public float3 screenWorld(ScreenCamera camera, float2 uv, float d)
{
    float4 world = mul(camera.inverseViewProjection, float4(uv * 2.0 - 1.0, d, 1.0));
    return world.xyz / world.w;
}

// Where a point projects onto a depth image, in xy, and its depth there, in z, and in w the depth
// the image holds there, or false where it lies behind the eye or off the picture.
bool onScreen<P : IPicture>(P depth, ScreenCamera camera, float3 q, out float4 at)
{
    at = float4(0.0);
    float4 clip = mul(camera.viewProjection, float4(q, 1.0));
    if (clip.w <= 1e-4)
        return false;
    float3 ndc = clip.xyz / clip.w;
    float2 uv = ndc.xy * 0.5 + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0))
        return false;
    at = float4(uv, ndc.z, depth.sampleAt(uv, 0).r);
    return true;
}

// The step toward a depth image's neighbor at uv + offset or uv - offset, the one whose point lies
// nearer p, so a texel at a thing's edge takes its neighbor on its own surface.
float3 pictureStep<P : IPicture>(P depth, ScreenCamera camera, float2 uv, float2 offset, float3 p)
{
    float3 ahead = screenWorld(camera, uv + offset, depth.sampleAt(uv + offset, 0).r) - p;
    float3 behind = p - screenWorld(camera, uv - offset, depth.sampleAt(uv - offset, 0).r);
    return dot(ahead, ahead) < dot(behind, behind) ? ahead : behind;
}

// The way out from the surface a depth image holds at uv, where its point is p, turned toward the
// eye, from its neighbors a texel on along each axis.
float3 pictureNormal<P : IPicture>(P depth, ScreenCamera camera, float2 uv, float3 p)
{
    float2 texel = 1.0 / max(depth.size(), float2(1.0));
    float3 n = cross(pictureStep(depth, camera, uv, float2(texel.x, 0.0), p), pictureStep(depth, camera, uv, float2(0.0, texel.y), p));
    float length = length(n);
    if (length < 1e-12)
        return normalize(camera.eye - p);
    n /= length;
    return dot(n, camera.eye - p) < 0.0 ? -n : n;
}

// Where a ray from p along way first meets the surface a depth image holds within length, in steps
// more finely spaced near p, as a world position, or false and how far it went where it left the
// picture or met nothing on it. A point behind the depth counts as on the surface while it is
// within two steps of it, or a quarter of a unit, so a ray passing behind a thing in front does not
// stop there, and the step it met the surface in is halved five times to find where, the ray met
// only where the depth does not leap between the halving's ends and the face there turns toward the
// ray. A ray that passed behind the depth so and then went off the picture is left at the last
// point it stood in front of it, for the field to find what it passed behind, since left where it
// went off it stood past a wall it had gone behind, and the field's trace from there lit a closed
// room with the lamp outside it, 92 levels where 8 at most, once the screen's probes' rays reached
// twice as far. One still behind the depth at its end is left there too, where counted as meeting
// nothing a ray that went into the floor before a wall ended under it, and the light from beyond
// read there was none (gi.screenprobe). One that went off the picture without passing behind the
// depth is left at its last point on it, since left at its first point off it the field's trace
// began under a floor the step had crossed.
public bool traceScreen<P : IPicture>(P depth, ScreenCamera camera, float3 p, float3 way, float length, int steps, out float3 hit, out float left)
{
    hit = p;
    left = length;
    float before = 0.0;
    float behindFrom = -1.0;
    for (int i = 0; i < steps; i++)
    {
        float s = float(i + 1) / float(steps);
        float t = length * s * s;
        float3 q = p + way * t;
        float4 at;
        if (!onScreen(depth, camera, q, at))
        {
            left = behindFrom >= 0.0 ? behindFrom : before;
            return false;
        }
        if (at.w < 1.0 && at.z > at.w)
        {
            float3 surface = screenWorld(camera, at.xy, at.w);
            float behind = distance(camera.eye, q) - distance(camera.eye, surface);
            if (behind < max(length / float(steps) * 2.0, 0.25))
            {
                float near = before, far = t;
                for (int k = 0; k < 5; k++)
                {
                    float middle = (near + far) * 0.5;
                    float4 m;
                    if (onScreen(depth, camera, p + way * middle, m) && m.w < 1.0 && m.z > m.w)
                    {
                        far = middle;
                        at = m;
                    }
                    else
                        near = middle;
                }
                hit = screenWorld(camera, at.xy, at.w);
                // Across a thing's edge the depth under the ray leaps from what lies behind to the
                // thing between the halving's two ends, however near they come, where on a surface,
                // however slanted, the ray's depth against it changes by no more than the halved
                // step allows, and a ray that crossed such an edge passed behind the thing and goes
                // on as any ray behind the depth. Taken as met, the rays of the far wall's probe in
                // the carried lamp's room that pass the thin green wall's end took its face the eye
                // sees, dark, where the reference meets the lit floor and wall beyond, and the probe
                // read 51% under the reference along its ways, where it reads 2% over
                // (gi.screenprobe); judged by how near the halving ends to the surface alone, a
                // mirror floor seen past a metal sphere at a slant drew in stripes.
                float4 inFront;
                bool leaps = false;
                if (onScreen(depth, camera, p + way * near, inFront))
                {
                    // Before the sky the depth leaps as far as it can.
                    float frontBy = inFront.w < 1.0
                        ? distance(camera.eye, p + way * near) - distance(camera.eye, screenWorld(camera, inFront.xy, inFront.w)) : -1e6;
                    float behindBy = distance(camera.eye, p + way * far) - distance(camera.eye, hit);
                    leaps = behindBy - frontBy > max(0.25, 8.0 * (far - near));
                }
                // A face the ray meets from behind, its way leaving the surface, is not where it
                // meets the thing, and the ray passed behind the thing as above. Taken as met, a ray
                // of a probe on the carried lamp's room's back wall past the green wall's end, the
                // lamp beyond the wall, met the face the eye sees, lit, where the face toward the
                // probe is dark, 1375% over the reference along it (gi.screenprobe), and green over
                // red read 0.97 on the back wall past the wall's edge and 1.44 on the floor where
                // the reference reads 0.74 and 0.99. Going on, the field reads the ray 57% under,
                // and green over red 0.83 and 1.08.
                if (!leaps && dot(pictureNormal(depth, camera, at.xy, hit), way) < 0.0)
                    return true;
            }
            if (behindFrom < 0.0)
                behindFrom = before;
        }
        before = t;
    }
    left = behindFrom >= 0.0 ? behindFrom : length;
    return false;
}
