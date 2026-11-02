// The fog's light and the share of the scene's light through it from the eye to the far edge of
// each slice of its volume, a thread a column of cells, each slice's light summed as it fades
// through the slice, the share through it gone, as Hillaire sums Frostbite's, so a thick slice
// gives no more light than its fog can scatter.
import fog;

// What fog_scatter.slang wrote, each cell's light times its extinction and the extinction.
[[vk::binding(0, 0)]]
Texture3D<float4> scattered;

[[vk::binding(1, 0)]] [format("rgba16f")]
RWTexture3D<float4> integrated;

[[vk::binding(2, 0)]]
ConstantBuffer<FogUniforms> fog;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(fog.grid.x) || id.y >= uint(fog.grid.y))
        return;
    float start = fog.eye.w, far = fog.density.w;
    float3 light = float3(0.0);
    float through = 1.0;
    for (uint k = 0; k < FogSlices; k++)
    {
        float4 cell = scattered.Load(int4(id.x, id.y, k, 0));
        float deep = sliceEdge(float(k) + 1.0, start, far) - sliceEdge(float(k), start, far);
        float kept = exp(-cell.a * deep);
        light += through * (cell.a > 1e-7 ? cell.rgb * (1.0 - kept) / cell.a : cell.rgb * deep);
        through *= kept;
        integrated[int3(id.x, id.y, k)] = float4(light, through);
    }
}
