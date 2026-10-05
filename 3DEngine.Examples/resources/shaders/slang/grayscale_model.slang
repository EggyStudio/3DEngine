// raylib's grayscale.fs, written in Slang for the model pass: the model's color, gray by NTSC's
// weights and unlit, as raylib's default model shader draws. raylib draws a model with the same
// shader as 2D, and here a model's shader imports the model pass, so it is a file of its own.
import modelpass;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The material's texture and color, linear in the pass, encoded again as raylib's bytes are
    float4 texelColor = baseColor(input);
    float3 encoded = linearToSrgb(texelColor.rgb);

    float gray = dot(encoded, float3(0.299, 0.587, 0.114));

    return float4(gray, gray, gray, texelColor.a);
}
