// Paints a moving plasma into a texture, a thread a texel, which the program then draws as any
// texture. The texture is declared as an image the shader writes, and set by its name.
uniform float time;
RWTexture2D<float4> image;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    float2 p = (float2(id.xy) + 0.5) / 256.0 * 8.0;
    float v = sin(p.x + time) + sin(p.y * 1.3 - time * 0.7) + sin((p.x + p.y) * 0.7 + time * 1.3)
            + sin(length(p - 4.0) * 1.5 - time * 2.0);
    image[id.xy] = float4(0.5 + 0.5 * sin(v), 0.5 + 0.5 * sin(v + 2.1), 0.5 + 0.5 * sin(v + 4.2), 1);
}
