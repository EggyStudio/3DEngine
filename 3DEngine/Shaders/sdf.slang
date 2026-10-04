// Draws text in a font loaded as FontType.Sdf, whose atlas holds in its alpha the distance from
// each texel to the glyph's edge, 0.5 on the edge. The edge is smoothed over the distance one pixel
// of the screen spans, so it stays a pixel wide however large the text is drawn.
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float distance = boundTexture.Sample(input.uv).a - 0.5;
    float perPixel = length(float2(ddx(distance), ddy(distance)));
    float alpha = smoothstep(-perPixel, perPixel, distance);
    if (alpha <= 0.0)
        discard;
    return float4(input.color.rgb, input.color.a * alpha);
}
