// Mixes the picture toward its brightness. param(0).x is how far, from 0 (color) to 1 (gray).
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = input.color * boundTexture.Sample(input.uv);
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    return float4(lerp(color.rgb, float3(gray, gray, gray), param(0).x), color.a);
}
