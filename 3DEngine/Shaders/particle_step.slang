// Steps one emitter's particles, a thread a particle. The slots the frame's new particles take are
// a run starting where the last run ended, wrapping round, so the oldest are replaced first, and
// each new one starts at the emitter, within a radius of it, with the emitter's velocity turned
// within a cone and its speed and life varied by a hash of its slot and the frame. Every other
// particle alive ages, falls under gravity and is slowed by drag, and one past its life is left dead. The first
// thread also writes what the draw reads of the emitter into the header before the particles.
// An emitter that collides finds where each particle's move meets a surface, through the scene's
// distance field where the field holds the place it moves to, or in the window's depth of the scene
// elsewhere, and bounces it off or ends its life there.
import scenefield;

struct Particle
{
    // xyz: where it is. w: how long it has lived.
    float4 positionAge;
    // xyz: how fast it moves. w: how long it lives, 0 for a dead one.
    float4 velocityLife;
};

// The header, two particles' worth of float4 values, then the particles.
[[vk::binding(0, 0)]]
RWStructuredBuffer<float4> slots;

struct StepPush
{
    float4 originAndSeconds;
    float4 velocityAndSpread;
    float4 gravityAndLife;
    // x: how much a life varies. y: how much a speed varies. z: the step's seed. w: the radius new particles start within.
    float4 variation;
    float4 startColor;
    float4 endColor;
    // x, y: the size at birth and at death. z: how bright an unlit one is. w: how it is drawn, as particles.slang unpacks it.
    float4 look;
    // x: the first slot to start a particle in. y: how many. z: how many slots there are, below
    // 2^21, then two bits of what a particle does at the scene's depth and eight of the share of
    // its speed a bounce keeps. w: the bits of the drag.
    uint4 counts;
};

// The view the scene's depth was drawn through, and whether one is bound this frame, in depth.x.
struct ParticleView
{
    float4x4 viewProjection;
    float4x4 inverseViewProjection;
    float4 depth;
};

// The depth of the window's meshes that cast shadows, at half its size, which particles meet.
[[vk::binding(0, 1)]]
Sampler2D sceneDepth;

[[vk::binding(1, 1)]]
ConstantBuffer<ParticleView> view;

// The scene's distance field and where its cascades lie, a field of nothing where it is off.
[[vk::binding(2, 1)]]
Sampler3D sceneField;

[[vk::binding(3, 1)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

// What a particle does where it meets the scene's depth, as ParticleCollision numbers it.
static const uint Bounce = 1;
static const uint Die = 2;

[[vk::push_constant]]
ConstantBuffer<StepPush> push;

static const uint Header = 4;

// A number from 0 to 1 that changes wholly with each of its inputs, Jarzynski and Olano's PCG.
float random(uint slot, uint seed, uint salt)
{
    uint state = slot * 747796405u + seed * 2891336453u + salt * 1181783497u + 2048419325u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return float((word >> 22u) ^ word) / 4294967295.0;
}

// A direction within angle of the way around, every direction in the cone as likely as another.
float3 within(float3 around, float angle, float u, float v)
{
    float z = 1.0 - u * (1.0 - cos(angle));
    float r = sqrt(max(0.0, 1.0 - z * z));
    float phi = v * 6.28318530718;
    float3 axis = normalize(around);
    float3 side = abs(axis.y) < 0.99 ? normalize(cross(axis, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 up = cross(side, axis);
    return normalize(side * (r * cos(phi)) + up * (r * sin(phi)) + axis * z);
}

// A point of the depth texture, at uv and the depth it holds, back in world space.
float3 surfaceAt(float2 uv, float depth)
{
    float4 world = mul(view.inverseViewProjection, float4(uv * 2.0 - 1.0, depth, 1.0));
    return world.xyz / world.w;
}

// Where a particle moving from was to now meets the depth of the scene: the surface behind it at
// its pixel, its normal facing the camera, and how far behind the surface's plane the particle is,
// or false where it did not cross that plane from the camera's side this step. A particle that
// was already behind the surface, as one passing behind a post, crosses nothing.
bool meets(float3 was, float3 now, out float3 surface, out float3 normal, out float behind)
{
    surface = now;
    normal = float3(0, 1, 0);
    behind = 0.0;
    float4 clip = mul(view.viewProjection, float4(now, 1.0));
    if (clip.w <= 0.0)
        return false;
    float3 ndc = clip.xyz / clip.w;
    float2 uv = ndc.xy * 0.5 + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0) || ndc.z > 1.0)
        return false;
    float depth = sceneDepth.SampleLevel(uv, 0).r;
    // Nothing drawn there, or the particle still in front of what is.
    if (depth >= 1.0 || ndc.z <= depth)
        return false;
    surface = surfaceAt(uv, depth);

    // The surface's normal from the texels beside it, turned toward the camera, which the point
    // on the near plane at the same pixel lies toward.
    uint width, height;
    sceneDepth.GetDimensions(width, height);
    float2 texel = 1.0 / float2(width, height);
    float2 right = uv + float2(texel.x, 0.0);
    float2 down = uv + float2(0.0, texel.y);
    float3 across = surfaceAt(right, sceneDepth.SampleLevel(right, 0).r) - surface;
    float3 along = surfaceAt(down, sceneDepth.SampleLevel(down, 0).r) - surface;
    float3 n = cross(across, along);
    if (dot(n, n) < 1e-12)
        return false;
    normal = normalize(n);
    if (dot(normal, surfaceAt(uv, 0.0) - surface) < 0.0)
        normal = -normal;

    behind = -dot(now - surface, normal);
    return dot(was - surface, normal) >= 0.0 && behind > 0.0;
}

// Where a particle moving from was to now meets a surface of the scene's distance field: the
// move stepped by the distance each point along it holds, until a point comes within a hundredth
// of a unit of a surface, its nearest point on that surface, the field's slope there as its
// normal, and how far behind the surface's plane now is. A particle that starts inside a mesh, as
// one given off within one, meets nothing, so it is not held there.
bool meetsField(float3 was, float3 now, out float3 surface, out float3 normal, out float behind)
{
    surface = now;
    normal = float3(0, 1, 0);
    behind = 0.0;
    if (fieldDistance(sceneField, fieldInfo, was) < 0.0)
        return false;
    float3 path = now - was;
    float length = max(sqrt(dot(path, path)), 1e-6);
    float3 way = path / length;
    float t = 0.0;
    for (int i = 0; i < 24; i++)
    {
        float3 p = was + way * min(t, length);
        float d = fieldDistance(sceneField, fieldInfo, p);
        if (d <= 0.01)
        {
            normal = fieldNormal(sceneField, fieldInfo, p);
            surface = p - normal * d;
            behind = -dot(now - surface, normal);
            return true;
        }
        if (t >= length)
            return false;
        t += max(d, 0.005);
    }
    return false;
}

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint capacity = push.counts.z & 0x1FFFFFu;
    if (id.x == 0)
    {
        slots[0] = push.startColor;
        slots[1] = push.endColor;
        slots[2] = push.look;
        slots[3] = float4(float(capacity), 0, 0, 0);
    }
    if (id.x >= capacity)
        return;

    uint at = Header + id.x * 2;
    float4 positionAge = slots[at];
    float4 velocityLife = slots[at + 1];
    float seconds = push.originAndSeconds.w;

    uint fromFirst = (id.x + capacity - push.counts.x) % capacity;
    if (fromFirst < push.counts.y)
    {
        uint seed = asuint(push.variation.z);
        float3 velocity = push.velocityAndSpread.xyz;
        float speed = length(velocity);
        float3 way = speed > 1e-6 ? velocity / speed : float3(0, 1, 0);
        way = within(way, push.velocityAndSpread.w, random(id.x, seed, 1), random(id.x, seed, 2));
        speed *= 1.0 + (random(id.x, seed, 3) * 2.0 - 1.0) * push.variation.y;
        float life = push.gravityAndLife.w * (1.0 + (random(id.x, seed, 4) * 2.0 - 1.0) * push.variation.x);
        float3 offset = within(float3(0, 1, 0), 3.14159265, random(id.x, seed, 5), random(id.x, seed, 6))
                        * push.variation.w * pow(random(id.x, seed, 7), 1.0 / 3.0);
        // Started part way through the step, so a burst of many in one frame does not leave as a sheet.
        float early = random(id.x, seed, 8) * seconds;
        float3 position = push.originAndSeconds.xyz + offset + way * speed * early;
        slots[at] = float4(position, early);
        slots[at + 1] = float4(way * speed, max(life, 1e-3));
        return;
    }

    if (velocityLife.w <= 0.0)
        return;
    float age = positionAge.w + seconds;
    if (age >= velocityLife.w)
    {
        slots[at + 1] = float4(0, 0, 0, 0);
        return;
    }
    // Drag takes a share of the speed away each second, by the exponential so a long step does
    // not turn it back.
    float3 moved = (velocityLife.xyz + push.gravityAndLife.xyz * seconds) * exp(-asfloat(push.counts.w) * seconds);
    float3 now = positionAge.xyz + moved * seconds;

    uint collision = (push.counts.z >> 21u) & 3u;
    float3 surface = now, normal = float3(0, 1, 0);
    float behind = 0.0;
    bool met = false;
    if (collision != 0u && fieldOn(fieldInfo) && fieldCascade(fieldInfo, now, 1.0) >= 0)
        met = meetsField(positionAge.xyz, now, surface, normal, behind);
    else if (collision != 0u && view.depth.x > 0.5)
        met = meets(positionAge.xyz, now, surface, normal, behind);
    if (met)
    {
        if (collision == Die)
        {
            slots[at + 1] = float4(0, 0, 0, 0);
            return;
        }
        // The speed into the surface turned back by the share kept, and the speed along it kept,
        // the particle lifted back out to the surface's plane where it went through, a hair in front.
        float bounce = float((push.counts.z >> 23u) & 255u) / 255.0;
        float into = dot(moved, normal);
        if (into < 0.0)
            moved -= (1.0 + bounce) * into * normal;
        now += normal * (behind + 0.01);
    }

    slots[at] = float4(now, age);
    slots[at + 1] = float4(moved, velocityLife.w);
}
