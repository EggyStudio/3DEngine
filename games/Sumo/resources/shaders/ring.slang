// The ring's clay, its color the texture ring_paint.slang paints each frame, read by where a point
// of the ring is rather than by the mesh's own coordinates, so the floor's square lies over the
// ring however the cylinder maps it. The sides below the floor are darker, and a ripple glows a
// little beside being lit.
import modelpass;

// The painted floor, and the half width of the square of the world it covers, centered on the ring.
Sampler2D paint;
uniform float extent;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 painted = paint.Sample(input.world.xz / (2.0 * extent) + 0.5);
    float side = 1.0 - saturate(normalize(input.normal).y);
    // The painter writes colors as the display shows them, which are lit in linear light.
    float3 albedo = pow(lerp(painted.rgb, float3(0.55, 0.42, 0.32), side), 2.2);
    return float4(lit(albedo, input) + painted.a * float3(1.0, 0.75, 0.35) * 0.5, 1);
}
