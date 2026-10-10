using System.Numerics;
using A = Assimp;

namespace Engine;

internal sealed partial class AssimpModelReader
{
    // -- Materials

    // Assimp's AI_DEFAULT_MATERIAL_NAME, the name of the material it adds to a file that has none.
    private const string AssimpDefaultMaterial = "DefaultMaterial";

    private static SceneMaterialPayload[] BuildMaterials(A.Scene aScene, bool readsOpacity)
    {
        if (aScene.MaterialCount == 0) return Array.Empty<SceneMaterialPayload>();
        var result = new SceneMaterialPayload[aScene.MaterialCount];
        for (int i = 0; i < aScene.MaterialCount; i++)
        {
            var m = aScene.Materials[i];
            var name = string.IsNullOrEmpty(m.Name) ? $"Material_{i}" : m.Name;

            // AssimpNetter exposes colors as System.Numerics.Vector4 directly. PBR-aware files
            // (glTF imported via Assimp, FBX from PBR exporters) populate metallic/roughness/
            // emissive too via $mat.* properties.
            // The material Assimp adds to a file that names none, as an OBJ with no MTL, is gray
            // (0.6), and raylib's default for such a file is white, so the texture a program sets
            // on it shows its own colors.
            var diffuse = m.HasColorDiffuse && m.Name != AssimpDefaultMaterial ? m.ColorDiffuse : Vector4.One;
            // glTF's own factors where the file has them. A Phong material has no metal, and its
            // shininess (0 to 1000) stands in for smoothness.
            float metallic = TryGetFloat(m, "$mat.metallicFactor", 0f);
            float roughness = TryGetFloat(m, "$mat.roughnessFactor", float.NaN);
            if (float.IsNaN(roughness))
                roughness = m.HasShininess ? 1f - MathF.Min(1f, MathF.Max(0f, m.Shininess) / 1000f) : 1f;

            var emissive = m.HasColorEmissive
                ? new Vector3(m.ColorEmissive.X, m.ColorEmissive.Y, m.ColorEmissive.Z)
                : Vector3.Zero;

            SceneTextureRef? baseTex = TryGetTexture(m, A.TextureType.Diffuse) ?? TryGetTexture(m, A.TextureType.BaseColor);
            // glTF's packed map, which Assimp lists as metalness, as roughness, or in older
            // versions as unknown. A Phong specular map is not one, since its channels mean
            // something else.
            SceneTextureRef? mrTex = TryGetTexture(m, A.TextureType.Metalness)
                                     ?? TryGetTexture(m, A.TextureType.Roughness)
                                     ?? TryGetTexture(m, A.TextureType.Unknown);
            SceneTextureRef? normalTex = TryGetTexture(m, A.TextureType.Normals) ?? TryGetTexture(m, A.TextureType.Height);
            SceneTextureRef? emissiveTex = TryGetTexture(m, A.TextureType.Emissive);
            SceneTextureRef? occlusionTex = TryGetTexture(m, A.TextureType.AmbientOcclusion) ?? TryGetTexture(m, A.TextureType.Lightmap);

            // glTF says how its alpha is meant, which Assimp passes on as material keys. A format
            // without them blends when its opacity is below one.
            var alphaMode = m.GetNonTextureProperty("$mat.gltf.alphaMode")?.GetStringValue() switch
            {
                "MASK" => SceneAlphaMode.Mask,
                "BLEND" => SceneAlphaMode.Blend,
                "OPAQUE" => SceneAlphaMode.Opaque,
                _ => readsOpacity && m.HasOpacity && m.Opacity < 1f ? SceneAlphaMode.Blend : SceneAlphaMode.Opaque,
            };
            var alphaCutoff = m.GetNonTextureProperty("$mat.gltf.alphaCutoff") is { } cutoff ? cutoff.GetFloatValue() : 0.5f;
            var opacity = readsOpacity && m.HasOpacity ? m.Opacity : 1f;
            diffuse.W = opacity * diffuse.W;

            // A format that does not say, as OBJ, draws both sides, as the engine always did. The
            // flag is read from its bytes, since glTF's importer stores it in a width IsTwoSided
            // misreads as false.
            bool doubleSided = m.GetNonTextureProperty("$mat.twosided") is not { } twoSided
                               || twoSided.RawData is not { } raw || raw.Any(b => b != 0);

            result[i] = new SceneMaterialPayload
            {
                Name = name,
                SourcePath = $"/Materials/{name}#{i}",
                BaseColorFactor = diffuse,
                BaseColorTexture = baseTex,
                MetallicFactor = metallic,
                RoughnessFactor = roughness,
                MetallicRoughnessTexture = mrTex,
                NormalTexture = normalTex,
                EmissiveFactor = emissive,
                EmissiveTexture = emissiveTex,
                OcclusionTexture = occlusionTex,
                AlphaMode = alphaMode,
                AlphaCutoff = alphaCutoff,
                DoubleSided = doubleSided,
                // glTF's KHR_materials_volume, which says how thick a part is where its mesh,
                // drawn as one sheet, says nothing of it.
                Thickness = Math.Max(0f, TryGetFloat(m, "$mat.volume.thicknessFactor", 0f)),
            };
        }
        return result;
    }

    private static SceneEmbeddedTexture ConvertEmbedded(A.EmbeddedTexture texture)
    {
        if (texture.IsCompressed)
            return new SceneEmbeddedTexture(texture.Filename, texture.CompressedData, texture.CompressedFormatHint ?? "", 0, 0, null);

        // Assimp stores raw pixels as BGRA.
        var texels = texture.NonCompressedData;
        var rgba = new byte[texels.Length * 4];
        for (int i = 0; i < texels.Length; i++)
        {
            rgba[i * 4] = texels[i].R;
            rgba[i * 4 + 1] = texels[i].G;
            rgba[i * 4 + 2] = texels[i].B;
            rgba[i * 4 + 3] = texels[i].A;
        }
        return new SceneEmbeddedTexture(texture.Filename, null, "", texture.Width, texture.Height, rgba);
    }

    private static SceneTextureRef? TryGetTexture(A.Material m, A.TextureType type)
    {
        if (m.GetMaterialTextureCount(type) == 0) return null;
        if (!m.GetMaterialTexture(type, 0, out var slot)) return null;
        if (string.IsNullOrEmpty(slot.FilePath)) return null;
        return new SceneTextureRef(
            AssetPath: slot.FilePath,
            UvSet: slot.UVIndex,
            WrapS: ConvertWrap(slot.WrapModeU),
            WrapT: ConvertWrap(slot.WrapModeV));
    }

    private static SceneWrapMode ConvertWrap(A.TextureWrapMode mode) => mode switch
    {
        A.TextureWrapMode.Wrap   => SceneWrapMode.Repeat,
        A.TextureWrapMode.Clamp  => SceneWrapMode.Clamp,
        A.TextureWrapMode.Mirror => SceneWrapMode.Mirror,
        A.TextureWrapMode.Decal  => SceneWrapMode.Black,
        _                        => SceneWrapMode.Repeat,
    };

    private static float TryGetFloat(A.Material m, string key, float fallback)
    {
        var prop = m.GetNonTextureProperty(key);
        if (prop is null) return fallback;
        try { return prop.GetFloatValue(); }
        catch { return fallback; }
    }

    private static bool TryGetBool(A.Material m, string baseName, A.TextureType texType, int texIndex, bool fallback)
    {
        var prop = m.GetProperty(baseName, texType, texIndex);
        if (prop is null) return fallback;
        try { return prop.GetIntegerValue() != 0; }
        catch { return fallback; }
    }
}
