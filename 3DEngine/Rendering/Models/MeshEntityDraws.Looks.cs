using System.Numerics;

namespace Engine;

// How a mesh and material an entity is drawn with become a look, its draw, instance and group, and
// the textures and shader values the look's draws take.
internal sealed partial class MeshEntityDraws
{
    // The look of a mesh and material, and whether it is drawn into the shadow maps alone, built the
    // first time an entity is drawn with them.
    private int LookFor(int mesh, in Material material, bool shadowOnly, in Frame frame)
    {
        if (_lookOf.TryGetValue((mesh, material, shadowOnly), out var index)) return index;
        if (_freeLooks.Count > 0) index = _freeLooks.Pop();
        else
        {
            if (_lookCount == _looks.Length) Array.Resize(ref _looks, _looks.Length * 2);
            index = _lookCount++;
        }
        _looks[index] = Build(mesh, material, shadowOnly, frame);
        _looks[index].Stamp = ++_stamps;
        return _lookOf[(mesh, material, shadowOnly)] = index;
    }

    // A look from a material, its draw with the world matrix and camera left for the frame, and the
    // values of a program's shader left for each frame's draws.
    private Look Build(int mesh, in Material material, bool shadowOnly, in Frame frame)
    {
        var pending = false;
        var (assets, textures) = (frame.Assets, frame.Textures);
        int Texture(Handle<TextureAsset> handle)
        {
            var id = TextureFor(handle, assets, textures);
            pending |= handle.IsValid && !_textures.ContainsKey(handle.Id);
            return id;
        }

        // A compute shader, or one unloaded, draws nothing, and the entity is drawn by the engine's own.
        var shader = material.Shader.IsValid && frame.Shaders?.Get(material.Shader.Id) is { } program
            && program.Stages.ContainsKey(ShaderStage.Fragment) ? material.Shader.Id : 0;
        var baseColor = Texture(material.BaseColorTexture);
        var draw = new ModelDraw(
            mesh,
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            Encoded(material.Albedo),
            baseColor,
            Metallic: material.MetallicFactor,
            Roughness: material.RoughnessFactor,
            NormalMap: Texture(material.NormalTexture),
            NormalScale: material.NormalScale,
            MetallicRoughnessMap: Texture(material.MetallicRoughnessTexture),
            Emission: material.EmissiveFactor,
            EmissiveMap: Texture(material.EmissiveTexture),
            OcclusionMap: Texture(material.OcclusionTexture),
            OcclusionStrength: material.OcclusionStrength,
            AlphaMode: material.AlphaMode,
            AlphaCutoff: material.AlphaCutoff,
            TextureTranslucent: baseColor != 0 && textures.IsTranslucent(baseColor),
            DoubleSided: material.DoubleSided,
            SubsurfaceShares: material.SubsurfaceColor,
            SubsurfaceRadius: Math.Max(0, material.SubsurfaceRadius),
            SubsurfaceThickness: Math.Max(0, material.SubsurfaceThickness),
            LightThrough: Math.Clamp(material.LightThrough, 0, 1),
            Shader: shader,
            ShadowOnly: shadowOnly);
        return new Look
        {
            Material = material,
            ShadowOnly = shadowOnly,
            Draw = draw,
            Instance = ModelRenderer.Instance.Of(draw),
            Group = draw.IsTranslucent ? TranslucentLook : shader != 0 ? ShadedLook : GroupOf(draw),
            Pending = pending,
            Built = _frame,
        };
    }

    // A draw with a program's shader given the values the program last set on it, copied once a
    // frame and shared by every entity drawn with it, which the pass batches by that copy.
    private ModelDraw WithValues(in ModelDraw draw)
    {
        if (draw.Shader == 0 || _shaders is null) return draw;
        if (!_values.TryGetValue(draw.Shader, out var values)) _values[draw.Shader] = values = E3D.ShaderValuesOf(_shaders, draw.Shader);
        return draw with { Uniforms = values.Uniforms, ShaderTextures = values.Textures };
    }

    // The group of an opaque draw's mesh, maps, sides, alpha mode and the light it scatters under
    // its surface, made the first time one is drawn.
    private int GroupOf(in ModelDraw draw)
    {
        var key = KeyOf(draw);
        if (_groupOf.TryGetValue(key, out var index)) return index;
        if (_freeGroups.Count > 0) _groups[index = _freeGroups.Pop()] = draw;
        else
        {
            _groups.Add(draw);
            index = _groups.Count - 1;
        }
        return _groupOf[key] = index;
    }

    private static GroupKey KeyOf(in ModelDraw draw) => new(draw.Mesh, draw.Texture, draw.NormalMap, draw.MetallicRoughnessMap, draw.EmissiveMap,
        draw.OcclusionMap, draw.DoubleSided, draw.AlphaMode, draw.Subsurface, draw.ShadowOnly);

    // Albedo is linear, and a draw's color is sRGB-encoded bytes, as the flat API's are.
    private Color Encoded(Vector4 albedo)
    {
        if (_colors.TryGetValue(albedo, out var known)) return known;
        var c = Vector4.Clamp(albedo, Vector4.Zero, Vector4.One);
        c = new Vector4(LinearToSrgb(c.X), LinearToSrgb(c.Y), LinearToSrgb(c.Z), c.W) * 255 + new Vector4(0.5f);
        return _colors[albedo] = new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)c.W);
    }

    private static float LinearToSrgb(float c) =>
        c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;

    private int TextureFor(Handle<TextureAsset> handle, Assets<TextureAsset>? assets, TextureStore textures)
    {
        if (!handle.IsValid) return 0;
        if (_textures.TryGetValue(handle.Id, out var id)) return id;
        if (assets is null || !assets.TryGet(handle, out var texture)) return 0; // still loading

        if (texture.Format != TextureFormat.Rgba8)
        {
            _textures[handle.Id] = 0;
            return 0;
        }
        var bytes = texture.Width * texture.Height * 4;
        var pixels = texture.Pixels.Length == bytes ? texture.Pixels : texture.Pixels[..bytes];
        // Trilinear, so a floor stretching away blends its mip levels rather than stepping between them.
        return _textures[handle.Id] = textures.Add(pixels, texture.Width, texture.Height, TextureFilter.Trilinear, mipmaps: true);
    }

    private void ForgetReloadedTextures(World world, TextureStore textures)
    {
        if (_textures.Count == 0) return;
        var events = world.ReadAssetEvents<TextureAsset>();
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind is not (AssetEventKind.Modified or AssetEventKind.Removed)) continue;
            if (!_textures.Remove(events[i].Id, out var id)) continue;
            if (id != 0) textures.Remove(id);
            Forget();
        }
    }
}
