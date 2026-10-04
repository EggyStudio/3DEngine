// The immediate pass's shapes drawn into the HDR frame with bloom on. Their colors and textures are
// sRGB-encoded, as raylib's are, so they are decoded here to the linear light the frame holds, and
// come out of the frame's tonemap and encoding as they went in, up to the tonemap's knee.
import engine;
import color;

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0)
{
    return transformVertex(position, uv, color);
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = input.color * boundTexture.Sample(input.uv);
    if (color.a <= 0.0)
        discard;
    return float4(srgbToLinear(color.rgb), color.a);
}
