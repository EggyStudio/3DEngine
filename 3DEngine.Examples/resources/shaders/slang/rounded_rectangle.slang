// raylib's rounded_rectangle.fs, written in Slang: a rectangle with rounded corners, its shadow and
// its border, by signed distance. raylib's base.vs is the engine's own vertex stage here.
//
// The rounded rectangle's distance is Iñigo Quilez's
// (https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm), as inobelar wrote it
// for a shader (https://www.shadertoy.com/view/fsdyzB), under the MIT license.
import engine;

uniform float4 rectangle;   // x, y, width and height, in pixels from the bottom as GLSL counts them
uniform float4 radius;      // Each corner's: top left, top right, bottom left, bottom right
uniform float4 color;

uniform float shadowRadius;
uniform float2 shadowOffset;
uniform float shadowScale;
uniform float4 shadowColor;

uniform float borderThickness;
uniform float4 borderColor;

// GLSL's gl_FragCoord counts from the bottom and SV_Position from the top, so the screen's height,
// which raylib's shader has no need of, turns it.
uniform float screenHeight;

float RoundedRectangleSDF(float2 fragCoord, float2 center, float2 halfSize, float4 cornerRadius)
{
    float2 fragFromCenter = fragCoord - center;

    // The corner's radius
    cornerRadius.xy = (fragFromCenter.y > 0.0) ? cornerRadius.xy : cornerRadius.zw;
    cornerRadius.x = (fragFromCenter.x < 0.0) ? cornerRadius.x : cornerRadius.y;

    float2 dist = abs(fragFromCenter) - halfSize + cornerRadius.x;
    return min(max(dist.x, dist.y), 0.0) + length(max(dist, 0.0)) - cornerRadius.x;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragCoord = float2(input.position.x, screenHeight - input.position.y);

    // The rectangle's distance,
    float2 halfSize = rectangle.zw * 0.5;
    float2 center = rectangle.xy + halfSize;
    float recSDF = RoundedRectangleSDF(fragCoord, center, halfSize, radius);

    // and its shadow's
    float2 shadowHalfSize = halfSize * shadowScale;
    float2 shadowCenter = center + shadowOffset;
    float shadowSDF = RoundedRectangleSDF(fragCoord, shadowCenter, shadowHalfSize, radius);

    float recFactor = smoothstep(1.0, 0.0, recSDF);
    float shadowFactor = smoothstep(shadowRadius, 0.0, shadowSDF);
    float borderFactor = smoothstep(0.0, 1.0, recSDF + borderThickness) * recFactor;

    float4 recColor = float4(color.rgb, color.a * recFactor);
    float4 shadowCol = float4(shadowColor.rgb, shadowColor.a * shadowFactor);
    float4 borderCol = float4(borderColor.rgb, borderColor.a * borderFactor);

    // The shadow, the rectangle over it and the border over both
    return lerp(lerp(shadowCol, recColor, recColor.a), borderCol, borderCol.a);
}
