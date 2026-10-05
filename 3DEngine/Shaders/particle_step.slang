// Steps one emitter's particles, a thread a particle. The slots the frame's new particles take are
// a run starting where the last run ended, wrapping round, so the oldest are replaced first, and
// each new one starts at the emitter, within a radius of it, with the emitter's velocity turned
// within a cone and its speed and life varied by a hash of its slot and the frame. Every other
// particle alive ages, falls under gravity and is slowed by drag, and one past its life is left dead. The first
// thread also writes what the draw reads of the emitter into the header before the particles.

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
    // x, y: the size at birth and at death. z: how bright an unlit one is. w: 1 when it is lit, plus 2 when it is textured.
    float4 look;
    // x: the first slot to start a particle in. y: how many. z: how many slots there are. w: the bits of the drag.
    uint4 counts;
};

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

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint capacity = push.counts.z;
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
    slots[at] = float4(positionAge.xyz + moved * seconds, age);
    slots[at + 1] = float4(moved, velocityLife.w);
}
