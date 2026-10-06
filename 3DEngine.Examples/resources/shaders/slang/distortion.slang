// raylib's distortion.fs, written in Slang: each half of a stereo picture bent as a head-mounted
// display's lens bends it, red, green and blue each by its own amount, and black past the lens.
import engine;

uniform float2 leftLensCenter;
uniform float2 rightLensCenter;
uniform float2 leftScreenCenter;
uniform float2 rightScreenCenter;
uniform float2 scale;
uniform float2 scaleIn;
uniform float4 deviceWarpParam;
uniform float4 chromaAbParam;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // The lens the pixel is under, and the middle of that eye's half
    float2 lensCenter = input.uv.x < 0.5 ? leftLensCenter : rightLensCenter;
    float2 screenCenter = input.uv.x < 0.5 ? leftScreenCenter : rightScreenCenter;
    float2 theta = (input.uv - lensCenter) * scaleIn;
    float rSq = theta.x * theta.x + theta.y * theta.y;
    float2 theta1 = theta * (deviceWarpParam.x + deviceWarpParam.y * rSq + deviceWarpParam.z * rSq * rSq + deviceWarpParam.w * rSq * rSq * rSq);
    float2 thetaBlue = theta1 * (chromaAbParam.z + chromaAbParam.w * rSq);
    float2 tcBlue = lensCenter + scale * thetaBlue;

    // Black past the lens's border
    if (any(clamp(tcBlue, screenCenter - float2(0.25, 0.5), screenCenter + float2(0.25, 0.5)) != tcBlue))
        return float4(0.0, 0.0, 0.0, 1.0);

    // Each color from where the lens bends it to
    float blue = boundTexture.Sample(tcBlue).b;
    float2 tcGreen = lensCenter + scale * theta1;
    float green = boundTexture.Sample(tcGreen).g;
    float2 thetaRed = theta1 * (chromaAbParam.x + chromaAbParam.y * rSq);
    float2 tcRed = lensCenter + scale * thetaRed;
    float red = boundTexture.Sample(tcRed).r;
    return float4(red, green, blue, 1.0);
}
