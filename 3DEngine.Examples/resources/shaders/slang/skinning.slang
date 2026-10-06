// raylib's skinning.vs and skinning.fs, written in Slang for the model pass. raylib's vertex stage
// poses each vertex by its bones' matrices, which the engine does on the GPU for every skinned mesh
// before its vertex stage, so this has raylib's fragment stage alone, the texture times the
// material's color, unlit.
import modelpass;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texel and the material's color as raylib reads them, encoded, where the pass decodes them
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texelColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 colDiffuse = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);

    // raylib's fragColor, the vertex's color, is white for a mesh without colors, as this one is.
    return texelColor*colDiffuse;
}
