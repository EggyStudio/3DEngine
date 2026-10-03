// Shades a model in flat bands, as a cartoon is, with a rim of light around its edge. Its uniforms
// are set by name from the program: how many bands, the rim's color, and where the viewer is.
import modelpass;

uniform float bands;
uniform float4 rimColor;
uniform float3 viewer;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 color = baseColor(input);
    float3 n = normalize(input.normal);

    // The light the model pass would give, cut into steps.
    float3 shaded = lit(float3(1.0), input);
    float level = floor(saturate(dot(shaded, float3(0.333))) * max(bands, 1.0) + 0.5) / max(bands, 1.0);

    // Strongest where the surface turns away from the viewer.
    float rim = pow(1.0 - saturate(dot(n, normalize(viewer - input.world))), 3.0);
    // The base color is linear, so it is encoded for the frame, and the rim, a color the program
    // set, is added as it is.
    return float4(toDisplay(color.rgb * level) + rimColor.rgb * rimColor.a * rim, color.a);
}
