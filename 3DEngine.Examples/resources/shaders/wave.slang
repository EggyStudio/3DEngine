// Bends the picture in a wave over time and darkens every other line, as an old screen does.
// param(0).x is the time in seconds.
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float time = param(0).x;
    float2 uv = input.uv;
    uv.x += sin(uv.y * 30.0 + time * 4.0) * 0.01;
    float4 color = input.color * boundTexture.Sample(uv);
    float scan = 0.85 + 0.15 * sin(input.uv.y * 600.0);
    return float4(color.rgb * scan, color.a);
}
