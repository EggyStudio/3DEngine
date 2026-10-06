// The color raylib's default shader gives a vertex, for models_point_rendering's points. A mesh
// here carries no color per vertex, so each point carries its hue in its first texture coordinate,
// a fraction of a turn, and is colored as ColorFromHSV colors it at full saturation and value.
import modelpass;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float hue = input.uv.x * 360.0;
    float3 k = fmod(float3(5.0, 3.0, 1.0) + hue / 60.0, 6.0);
    float3 color = 1.0 - max(0.0, min(min(k, 4.0 - k), 1.0));
    // As ColorFromHSV rounds each channel down to a byte
    return float4(floor(color * 255.0) / 255.0, 1.0);
}
