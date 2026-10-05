// raylib's grayscale.fs, written in Slang: the color drawn, gray by NTSC's weights.
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // The texel times the color drawn with, raylib's colDiffuse being white in 2D
    float4 texelColor = boundTexture.Sample(input.uv) * input.color;

    float gray = dot(texelColor.rgb, float3(0.299, 0.587, 0.114));

    return float4(gray, gray, gray, texelColor.a);
}
