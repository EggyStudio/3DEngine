// A marble of swirled glass in a player's color, the swirl drawn from the sphere's own coordinates
// so it rolls with the marble, and a rim of the color round its edge, lit as the model pass lights
// its meshes. The program sets the two colors before each marble it draws.
import modelpass;

// The player's color and the swirl's, as the program's colors are, encoded for the display.
uniform float4 tint;
uniform float4 swirl;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float band = sin(input.uv.x * 6.2831 * 3.0 + sin(input.uv.y * 6.2831 * 2.0) * 2.5);
    float3 albedo = lerp(pow(tint.rgb, 2.2), pow(swirl.rgb, 2.2), smoothstep(0.3, 0.7, band * 0.5 + 0.5));

    // Strongest where the surface turns away from the eye.
    float rim = pow(1.0 - saturate(dot(normalize(input.normal), normalize(input.toEye))), 3.0);
    return float4(lit(albedo, input) + tint.rgb * rim * 0.7, 1);
}
