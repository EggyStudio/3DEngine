// raylib's alpha_discard.fs, written in Slang: a texel with no alpha is discarded rather than drawn
// clear, so the depth it would write does not hide what is drawn behind it after.
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 texelColor = boundTexture.Sample(input.uv);
    if (texelColor.a == 0.0) discard;
    return texelColor*input.color;
}
