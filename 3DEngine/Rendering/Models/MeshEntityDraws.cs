using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Records every entity with a <see cref="Mesh"/> and a <see cref="Material"/> into the frame's
/// <see cref="ModelDrawList"/>, so mesh entities are drawn by the same pass as <c>DrawModel</c>.
/// </summary>
/// <remarks>
/// <para>
/// Runs in <see cref="Stage.Render"/> and draws through the first entity with a
/// <see cref="Camera"/>. Without one, mesh entities are not drawn, as before.
/// </para>
/// <para>
/// A mesh's arrays are uploaded to <see cref="MeshStore"/> the first time they are drawn and freed
/// on the first frame no entity draws them. A material's base color texture is copied from the
/// asset store into <see cref="TextureStore"/> once it has loaded, and copied again when the asset
/// is reloaded. Only the first mip level of an RGBA8 texture is copied, and the GPU makes the rest.
/// </para>
/// <para>
/// An entity whose material blends, with alpha below 1 in its albedo or base color texture, shows
/// what is behind it, so it is recorded after the opaque ones, from the farthest from the camera
/// to the nearest, which the model pass keeps.
/// </para>
/// </remarks>
public sealed class MeshEntityDraws
{
    private readonly Dictionary<Vector3[], int> _meshes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Vector3[]> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AssetId, int> _textures = [];
    private readonly List<(float Distance, ModelDraw Draw)> _translucent = [];

    // The frame's opaque draws, handed to the draw list under one lock rather than one each.
    private readonly List<ModelDraw> _opaque = [];

    // Each albedo's sRGB bytes, since entities share a few materials and three powers an entity
    // cost a measurable part of the frame (RENDERING.md section 6). Forgotten past a few thousand.
    private readonly Dictionary<Vector4, Color> _colors = [];

    /// <summary>How many meshes are uploaded for entities.</summary>
    public int MeshCount => _meshes.Count;

    /// <summary>How many texture assets are copied for entities.</summary>
    public int TextureCount => _textures.Count;

    /// <summary>The system, for <see cref="Stage.Render"/>.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || !world.TryGetResource<ModelDrawList>(out var draws)) return;
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        self.Record(world, ecs, draws);
    }

    private void Record(World world, EcsWorld ecs, ModelDrawList draws)
    {
        var meshes = world.Resource<MeshStore>();
        var textures = world.Resource<TextureStore>();
        world.TryGetResource<Assets<Texture>>(out var assets);
        ForgetReloadedTextures(world, textures);

        _seen.Clear();
        _translucent.Clear();
        _opaque.Clear();
        if (_colors.Count > 4096) _colors.Clear();
        if (ecs.Count<Mesh>() > 0 && FirstCamera(world, ecs) is { } camera)
        {
            var (viewProjection, eye) = camera;
            // Entities spawned together usually share one positions array, so the mesh of the
            // entity before is checked first.
            Vector3[]? lastPositions = null;
            var id = 0;
            foreach (var (entity, mesh) in ecs.Query<Mesh>())
            {
                if (mesh.Positions is not { Length: >= 3 } || !ecs.TryGet(entity, out Material material)) continue;

                if (!ReferenceEquals(mesh.Positions, lastPositions))
                {
                    lastPositions = mesh.Positions;
                    _seen.Add(mesh.Positions);
                    if (!_meshes.TryGetValue(mesh.Positions, out id))
                        _meshes[mesh.Positions] = id = meshes.Add(Vertices(mesh), Sequence(mesh.Positions.Length / 3 * 3));
                }

                var placed = TransformPropagation.WorldMatrix(ecs, entity);
                var baseColor = TextureFor(material.BaseColorTexture, assets, textures);
                var draw = new ModelDraw(
                    id,
                    placed,
                    viewProjection,
                    Encoded(material.Albedo),
                    baseColor,
                    Metallic: material.MetallicFactor,
                    Roughness: material.RoughnessFactor,
                    NormalMap: TextureFor(material.NormalTexture, assets, textures),
                    NormalScale: material.NormalScale,
                    MetallicRoughnessMap: TextureFor(material.MetallicRoughnessTexture, assets, textures),
                    Emission: material.EmissiveFactor,
                    EmissiveMap: TextureFor(material.EmissiveTexture, assets, textures),
                    OcclusionMap: TextureFor(material.OcclusionTexture, assets, textures),
                    OcclusionStrength: material.OcclusionStrength,
                    AlphaMode: material.AlphaMode,
                    AlphaCutoff: material.AlphaCutoff,
                    TextureTranslucent: baseColor != 0 && textures.IsTranslucent(baseColor));
                if (draw.IsTranslucent) _translucent.Add((Vector3.DistanceSquared(placed.Translation, eye), draw));
                else _opaque.Add(draw);
            }

            _translucent.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));
            foreach (var (_, draw) in _translucent) _opaque.Add(draw);
            draws.AddRange(CollectionsMarshal.AsSpan(_opaque));
        }

        // A mesh no entity drew this frame was despawned or replaced.
        if (_meshes.Count > _seen.Count)
            foreach (var positions in _meshes.Keys.Where(p => !_seen.Contains(p)).ToArray())
            {
                meshes.Remove(_meshes[positions]);
                _meshes.Remove(positions);
            }
    }

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

    /// <summary>World to clip space through the first camera entity, as <see cref="CameraExtract"/> builds it, and where that camera is.</summary>
    private static (Matrix4x4 ViewProjection, Vector3 Eye)? FirstCamera(World world, EcsWorld ecs)
    {
        foreach (var (entity, camera) in ecs.Query<Camera>())
        {
            // The window's shape, or with no window (an offscreen run) the size the config asked
            // for, which is what the frames are drawn at. Assuming square there stretched every
            // mesh entity in an offscreen capture while the 2D drawing beside it was right.
            var (width, height) = world.TryGetResource<AppWindow>(out var window)
                ? (window.Sdl.Width, window.Sdl.Height)
                : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
            var aspect = height > 0 ? (float)width / height : 1f;
            var (view, projection) = CameraExtract.Matrices(ecs, entity, camera, aspect);
            return (view * projection, TransformPropagation.WorldMatrix(ecs, entity).Translation);
        }
        return null;
    }

    private int TextureFor(Handle<Texture> handle, Assets<Texture>? assets, TextureStore textures)
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
        return _textures[handle.Id] = textures.Add(pixels, texture.Width, texture.Height, mipmaps: true);
    }

    private void ForgetReloadedTextures(World world, TextureStore textures)
    {
        if (_textures.Count == 0) return;
        var events = world.ReadAssetEvents<Texture>();
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind is not (AssetEventKind.Modified or AssetEventKind.Removed)) continue;
            if (!_textures.Remove(events[i].Id, out var id)) continue;
            if (id != 0) textures.Remove(id);
        }
    }

    private static ModelVertex[] Vertices(in Mesh mesh)
    {
        var count = mesh.Positions.Length / 3 * 3;
        var vertices = new ModelVertex[count];
        for (int i = 0; i < count; i += 3)
        {
            var (a, b, c) = (mesh.Positions[i], mesh.Positions[i + 1], mesh.Positions[i + 2]);
            // A triangle with no normals of its own is lit flat, by the normal of its face.
            var face = Vector3.Cross(b - a, c - a);
            face = face.LengthSquared() > 0 ? Vector3.Normalize(face) : Vector3.UnitY;
            for (int k = 0; k < 3; k++)
            {
                var n = mesh.Normals is { } normals && i + k < normals.Length ? normals[i + k] : face;
                var uv = mesh.Uvs is { } uvs && i + k < uvs.Length ? uvs[i + k] : Vector2.Zero;
                vertices[i + k] = new ModelVertex(mesh.Positions[i + k], n, uv);
            }
        }
        return vertices;
    }

    private static uint[] Sequence(int count)
    {
        var indices = new uint[count];
        for (uint i = 0; i < count; i++) indices[i] = i;
        return indices;
    }
}
