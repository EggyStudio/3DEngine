// raylib's reload.fs, written in Slang: a background that shifts with time and a circle at the
// pointer. Edit this file in the program's output folder while the example runs, and a click, or
// every frame with automatic reloading on, compiles it again.
import engine;

uniform float2 resolution;      // The view's size in pixels
uniform float2 mouse;           // The pointer, in pixels
uniform float time;             // Seconds run

float4 DrawCircle(float2 fragCoord, float2 position, float radius, float3 color)
{
    float d = length(position - fragCoord) - radius;
    float t = clamp(d, 0.0, 1.0);
    return float4(color, 1.0 - t);
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // The pixel as GLSL's gl_FragCoord counts it, from the bottom
    float2 fragCoord = float2(input.position.x, resolution.y - input.position.y);
    float2 position = float2(mouse.x, resolution.y - mouse.y);
    float radius = 40.0;

    // The background,
    float4 colorA = float4(0.2, 0.2, 0.8, 1.0);
    float4 colorB = float4(1.0, 0.7, 0.2, 1.0);
    float4 layer1 = lerp(colorA, colorB, abs(sin(time * 0.1)));

    // and the circle over it
    float3 color = float3(0.9, 0.16, 0.21);
    float4 layer2 = DrawCircle(fragCoord, position, radius, color);

    return lerp(layer1, layer2, layer2.a);
}
