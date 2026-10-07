// Paints the ring's floor each frame, a thread a texel: packed clay, the straw circle near its edge
// and the two lines the players start behind, with a ripple running out from each bump of the
// marbles. The alpha carries how strong the ripples are there, which ring.slang lights.
uniform float time;
// Half the width of the square of the world the texture covers, and the ring's radius, in units.
uniform float extent;
uniform float radius;
// Texels a side.
uniform int size;
// The last eight bumps, each where it was on the floor, when, and how hard, 0 for none.
StructuredBuffer<float4> bumps;
RWTexture2D<float4> image;

float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(size) || id.y >= uint(size))
        return;

    // The texel's place on the floor, its x the world's x and its y the world's z.
    float2 p = ((float2(id.xy) + 0.5) / float(size) * 2.0 - 1.0) * extent;
    float r = length(p);
    float3 color = float3(0.78, 0.62, 0.42) * (0.9 + 0.1 * hash(floor(p * 6.0)));
    color = lerp(color, float3(0.92, 0.84, 0.58), smoothstep(0.2, 0.05, abs(r - (radius - 0.7))));
    if (abs(abs(p.x) - 1.3) < 0.07 && abs(p.y) < 0.7)
        color = float3(0.97, 0.96, 0.92);

    // Each bump's ripple, a ring running out at seven units a second and fading over a second and a half.
    float glow = 0.0;
    for (int i = 0; i < 8; i++)
    {
        float4 bump = bumps[i];
        float age = time - bump.z;
        if (bump.w <= 0.0 || age < 0.0 || age > 1.5)
            continue;
        float off = abs(length(p - bump.xy) - age * 7.0);
        glow += bump.w * smoothstep(0.6, 0.0, off) * (1.0 - age / 1.5);
    }
    glow = saturate(glow);
    image[id.xy] = float4(lerp(color, float3(1.0, 0.86, 0.5), glow * 0.7), glow);
}
