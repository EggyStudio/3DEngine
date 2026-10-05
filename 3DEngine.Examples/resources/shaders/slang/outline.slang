// raylib's outline.fs, written in Slang: a texture with a line around its opaque parts, found
// where any of four texels a step away has alpha.
import engine;

uniform float2 textureSize;
uniform float outlineSize;
uniform float4 outlineColor;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 texel = boundTexture.Sample(input.uv);
    float2 texelScale = float2(outlineSize / textureSize.x, outlineSize / textureSize.y);

    // The alpha of the four texels at the corners, a step away
    float4 corners;
    corners.x = boundTexture.Sample(input.uv + float2(texelScale.x, texelScale.y)).a;
    corners.y = boundTexture.Sample(input.uv + float2(texelScale.x, -texelScale.y)).a;
    corners.z = boundTexture.Sample(input.uv + float2(-texelScale.x, texelScale.y)).a;
    corners.w = boundTexture.Sample(input.uv + float2(-texelScale.x, -texelScale.y)).a;

    float outline = min(dot(corners, float4(1.0)), 1.0);
    float4 color = lerp(float4(0.0), outlineColor, outline);
    return lerp(color, texel, texel.a);
}
