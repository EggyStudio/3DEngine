// Draws the lines and triangles the flat API's Draw calls record: each vertex's color times the
// batch's texture, which is a white pixel for untextured shapes. A shader loaded with LoadShader
// replaces fragmentMain, and vertexMain when it has one, for the draws inside BeginShaderMode.
import engine;

[shader("vertex")]
VertexOutput vertexMain(float3 position : POSITION, float2 uv : TEXCOORD0, float4 color : COLOR0)
{
    return transformVertex(position, uv, color);
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // In 3D a texel with no coverage is discarded rather than blended, so the empty corners of a
    // sprite or billboard write no depth and do not hide what is drawn behind them later. In 2D it
    // is blended as raylib blends it, which an additive or custom blend mode reads. The pass says
    // which in param 0's x.
    float4 color = input.color * boundTexture.Sample(input.uv);
    if (color.a <= 0.0 && param(0).x != 0.0)
        discard;
    return color;
}
