// The air's transmittance by height and zenith, the share of the light that crosses the air from a
// height toward a way out to its top, each texel at its grid point, as the Sky module's SkyLight
// makes its table.
import atmosphere;

[[vk::binding(0, 0)]] [format("rgba16f")] RWTexture2D<float4> table;
[[vk::push_constant]] ConstantBuffer<AirPush> push;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= TransmittanceAngles || id.y >= TransmittanceHeights)
        return;
    float row = float(id.y) / (TransmittanceHeights - 1);
    float height = push.planet.y * row * row;
    float mu = -1.0 + 2.0 * float(id.x) / (TransmittanceAngles - 1);
    table[id.xy] = float4(transmittance(push, height, mu), 1.0);
}
