// raylib's gbuffer.vs and gbuffer.fs, written in Slang for the model pass: each pixel's position
// and normal in the world, and its color with its specular strength, into the three textures of
// a G-buffer. raylib's diffuseTexture and specularTexture are both left on the first texture unit,
// so both read the material's own texture, as they do here.
import modelpass;

struct GBufferOutput
{
    float4 position : SV_Target0;
    float4 normal : SV_Target1;
    float4 albedoSpec : SV_Target2;
};

[shader("fragment")]
GBufferOutput fragmentMain(ModelVertexOutput input)
{
    GBufferOutput output;
    // store the fragment position vector in the first gbuffer texture
    output.position = float4(input.world, 1.0);
    // also store the per-fragment normals into the gbuffer
    output.normal = float4(normalize(input.normal), 1.0);
    // and the diffuse per-fragment color, encoded as raylib reads it, where the pass decodes it
    float4 texel = boundTexture.Sample(input.uv);
    output.albedoSpec.rgb = linearToSrgb(texel.rgb);
    // store specular intensity in gAlbedoSpec's alpha component
    output.albedoSpec.a = linearToSrgb(texel.rgb).r;
    return output;
}
