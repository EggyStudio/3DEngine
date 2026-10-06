// raylib's lightmap.vs and lightmap.fs, written in Slang for the model pass: the material's texture
// at the mesh's texture coordinate times the lightmap, its metalness map, at the second.
import modelpass;

[shader("fragment")]
float4 fragmentMain(ModelStreamsOutput input) : SV_Target
{
    // Texel color fetching from texture sampler, encoded as raylib reads it, where the pass decodes
    // it. The lightmap is a render texture, read as it was drawn.
    float4 sampled = boundTexture.Sample(input.base.uv);
    float4 texelColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 texelColor2 = metallicRoughnessMap.Sample(input.uv2);

    return texelColor*texelColor2;
}
