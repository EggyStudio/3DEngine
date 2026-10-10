using System.Numerics;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Translates a backend-agnostic <see cref="Scene"/> snapshot into ECS entities and
/// components. Encapsulates the spawn-time policy described on
/// <see cref="Scene"/>: apply the scene's basis change + unit scale once at the root,
/// recurse with parent-multiplied world matrices, and turn payload bags into runtime
/// components (mesh / material / camera).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a class, not a system:</b> spawning is request-driven (load asset, then spawn
/// once). A synchronous helper is usable from anywhere, startup behaviors and tests among
/// them, while <see cref="SceneSpawnSystem"/> is a thin driver that polls for
/// <see cref="SpawnSceneRequest"/> components and calls the spawner when their
/// <see cref="Handle{T}"/> resolves.
/// </para>
/// <para>
/// <b>Coordinate / unit policy:</b> per <see cref="Scene"/>, the reader preserves the
/// source basis &amp; units verbatim and the spawner applies a single root-level
/// basis-change matrix. <see cref="ComputeRootMatrix"/> derives that matrix from the
/// scene's <see cref="Scene.SourceCoordinateSystem"/> and
/// <see cref="Scene.SourceMetersPerUnit"/>. Z-up &#x2192; Y-up is a -90&#xB0; rotation
/// around the X axis (mapping <c>(x,y,z)</c> to <c>(x, z, -y)</c>); units convert by
/// uniform scale of <c>sourceMetersPerUnit / targetMetersPerUnit</c>.
/// </para>
/// <para>
/// <b>v1 mesh contract:</b> the runtime <see cref="Mesh"/> still consumes a flat
/// <see cref="Vector3"/> array; the spawner de-indexes
/// <see cref="SceneMeshPayload.Indices"/> into a flat positions array of size
/// <c>Indices.Length</c> at spawn time, matching the documented v1 contract on
/// <see cref="SceneMeshPayload"/>. Richer fields (normals / UVs / tangents) ride along on
/// the payload for the future renderer-side upgrade.
/// </para>
/// </remarks>
internal static class SceneSpawner
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <summary>
    /// Spawns <paramref name="scene"/> into <paramref name="ecs"/>, returning the IDs of
    /// every entity created. The order matches a depth-first traversal of
    /// <see cref="Scene.Roots"/>.
    /// </summary>
    /// <remarks>
    /// Loads no textures, so its materials draw with their factors alone. A spawn that loads them
    /// goes through <see cref="SpawnTaking"/>, which hands back each load it took, for
    /// <see cref="AssetRelease"/> to hold by the spawned entities and give back once they are gone.
    /// </remarks>
    /// <param name="ecs">Target ECS world.</param>
    /// <param name="scene">Source snapshot. Not mutated.</param>
    /// <param name="settings">Spawn-time policy (purpose mask, defaults). May be <c>null</c>; <see cref="SceneSpawnSettings.Default"/> is used.</param>
    /// <param name="sceneAssetId">Optional source asset id, copied into <see cref="SceneInstance.SceneAssetId"/> on every spawned entity.</param>
    /// <param name="materialLibrary">
    /// Optional <see cref="MaterialLibrary"/> used to register every spawned
    /// <see cref="SceneMaterialPayload"/> as a <see cref="MaterialDescription"/>. The
    /// returned <see cref="MaterialHandle"/> is stored on <see cref="Material.Handle"/>
    /// so the renderer can key per-material pipelines, descriptor sets and uniform buffers by it. <c>null</c> leaves the handle as
    /// <see cref="MaterialHandle"/> default; the renderer then falls back to the
    /// shared static-white pipeline.
    /// </param>
    /// <returns>The list of spawned entity IDs (depth-first order). Empty when nothing matched the filters.</returns>
    public static List<int> Spawn(
        EcsWorld ecs,
        Scene scene,
        SceneSpawnSettings? settings = null,
        ulong sceneAssetId = 0,
        MaterialLibrary? materialLibrary = null) =>
        SpawnTaking(ecs, scene, settings, sceneAssetId, assetServer: null, sceneSourcePath: null, materialLibrary, taken: []);

    /// <summary>
    /// Spawns <paramref name="scene"/> as <see cref="Spawn"/> does, loading the textures its
    /// materials name from <paramref name="assetServer"/> and adding each load to
    /// <paramref name="taken"/>, which the caller gives back once the spawned entities are gone.
    /// </summary>
    /// <param name="ecs">Target ECS world.</param>
    /// <param name="scene">Source snapshot. Not mutated.</param>
    /// <param name="settings">Spawn-time policy, <see cref="SceneSpawnSettings.Default"/> where <c>null</c>.</param>
    /// <param name="sceneAssetId">The source asset's id, copied into <see cref="SceneInstance.SceneAssetId"/> on every spawned entity.</param>
    /// <param name="assetServer">
    /// Resolves texture references on <see cref="SceneMaterialPayload"/> (using
    /// <see cref="TextureLoadExtensions"/>' sRGB / linear / mips conventions) into the
    /// <see cref="Material"/> texture slots. <c>null</c> skips texture loads and logs once a
    /// process that they were left out.
    /// </param>
    /// <param name="sceneSourcePath">
    /// Resolved <see cref="SceneAsset.SourcePath"/> of the source file
    /// (e.g. <c>"models/hero.glb"</c>). Used as the directory root for any relative
    /// texture paths in the scene's material payloads. <c>null</c> = no prefix.
    /// </param>
    /// <param name="materialLibrary">Registers each spawned material, as on <see cref="Spawn"/>.</param>
    /// <param name="taken">Receives the id of each texture loaded, a load of the asset server's each.</param>
    internal static List<int> SpawnTaking(
        EcsWorld ecs,
        Scene scene,
        SceneSpawnSettings? settings,
        ulong sceneAssetId,
        AssetServer? assetServer,
        string? sceneSourcePath,
        MaterialLibrary? materialLibrary,
        List<AssetId> taken)
    {
        ArgumentNullException.ThrowIfNull(ecs);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(taken);
        settings ??= SceneSpawnSettings.Default;

        var entities = new List<int>();
        var rootMatrix = ComputeRootMatrix(scene, settings);
        var ctx = new SpawnContext(assetServer, sceneSourcePath, materialLibrary, scene, taken);

        foreach (var node in scene.Roots)
            SpawnRecursive(ecs, node, rootMatrix, settings, sceneAssetId, entities, ctx);

        Logger.Debug($"SceneSpawner: spawned {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")} from scene '{scene.Name}'.");
        return entities;
    }

    private readonly struct SpawnContext
    {
        public AssetServer? Server { get; }
        public string? SceneDirectory { get; }
        public MaterialLibrary? Materials { get; }
        public Scene Scene { get; }
        public string SceneKey { get; }
        public List<AssetId> Taken { get; }
        public SpawnContext(AssetServer? server, string? sceneSourcePath, MaterialLibrary? materials, Scene scene, List<AssetId> taken)
        {
            Server = server;
            Taken = taken;
            SceneDirectory = ResolveSceneDirectory(sceneSourcePath);
            Materials = materials;
            Scene = scene;
            SceneKey = (sceneSourcePath ?? scene.Name).Replace('\\', '/').TrimStart('/');
        }

        private static string? ResolveSceneDirectory(string? sceneSourcePath)
        {
            if (string.IsNullOrEmpty(sceneSourcePath)) return null;
            // SceneAsset.SourcePath is normalized to "/"; pull the directory portion
            // (forward-slash). System.IO.Path's behavior matches both separators.
            var normalized = sceneSourcePath.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? string.Empty : normalized[..slash];
        }
    }

    /// <summary>
    /// Computes the root-level world matrix that bakes the scene's source basis
    /// (<see cref="SceneCoordinateSystem"/>) and unit scale into a single transform.
    /// Exposed for testing and for callers positioning a spawned scene
    /// (compose with their own placement matrix before passing in).
    /// </summary>
    public static Matrix4x4 ComputeRootMatrix(Scene scene, SceneSpawnSettings settings)
    {
        // Unit conversion: source mpu = N means "1 source unit is N meters". Scaling by
        // (sourceMpu / targetMpu) converts the vertex data to target units.
        var unitScale = (float)(scene.SourceMetersPerUnit / Math.Max(settings.TargetMetersPerUnit, 1e-9));
        var scale = Matrix4x4.CreateScale(unitScale);

        // Basis change: if the source is Z-up and the target is Y-up, rotate -90deg around
        // the X axis so the source +Z aligns with target +Y. Same-basis is identity.
        var basis = Matrix4x4.Identity;
        if (scene.SourceCoordinateSystem == SceneCoordinateSystem.ZUp &&
            settings.TargetCoordinateSystem == SceneCoordinateSystem.YUp)
        {
            basis = Matrix4x4.CreateRotationX(-MathF.PI / 2f);
        }

        // Composition: child = local * scale * basis * placement
        // (rightmost gets applied last in System.Numerics' row-vector convention).
        return scale * basis * settings.Placement;
    }

    private static void SpawnRecursive(
        EcsWorld ecs,
        SceneNode node,
        Matrix4x4 parentWorld,
        SceneSpawnSettings settings,
        ulong sceneAssetId,
        List<int> entities,
        SpawnContext ctx,
        int parentEntity = 0,
        Matrix4x4 parentEntityWorld = default)
    {
        var localMatrix = ComposeLocalMatrix(node.LocalTransform);
        var worldMatrix = localMatrix * parentWorld;

        bool include = node.Enabled
                       && settings.IncludePurposes.HasFlag(PurposeFlag(node.Purpose));

        var spawned = parentEntity;
        var spawnedWorld = parentEntityWorld;
        if (include && HasSpawnablePayload(node))
        {
            var entity = ecs.Spawn();
            entities.Add(entity);
            spawned = entity;
            spawnedWorld = worldMatrix;

            // A child's transform is relative to the entity it hangs from, which propagation
            // composes back into its world matrix. A root keeps the world matrix itself.
            var relative = worldMatrix;
            if (parentEntity != 0 && Matrix4x4.Invert(parentEntityWorld, out var inverse))
                relative = worldMatrix * inverse;
            ecs.Add(entity, DecomposeToTransform(relative));
            ecs.Add(entity, new Name(node.Name));
            if (parentEntity != 0) ecs.SetParent(entity, parentEntity);

            if (settings.AttachSceneInstanceMarker)
            {
                ecs.Add(entity, new SceneInstance
                {
                    SceneAssetId = sceneAssetId,
                    SourcePath = node.SourcePath,
                });
            }

            AttachComponents(ecs, entity, node, settings, ctx, sceneAssetId, entities);
        }

        // A node without a payload spawns nothing, so its children hang from the nearest ancestor that did.
        foreach (var child in node.Children)
            SpawnRecursive(ecs, child, worldMatrix, settings, sceneAssetId, entities, ctx, spawned, spawnedWorld);
    }

    private static bool HasSpawnablePayload(SceneNode node)
    {
        // A node without any payload is a hierarchy-only group; the v1 spawner doesn't
        // create empty entities for it. (The accumulated transform is still composed for
        // descendants through parentWorld, and no entity is made for it.)
        foreach (var c in node.Components)
            if (c is SceneMeshPayload or SceneCameraPayload or SceneLightPayload or SceneMaterialPayload)
                return true;
        return false;
    }

    private static void AttachComponents(EcsWorld ecs, int entity, SceneNode node, SceneSpawnSettings settings, SpawnContext ctx,
        ulong sceneAssetId, List<int> entities)
    {
        // A node's meshes, each with the material the reader put after it. A file with several
        // materials on one object, as an OBJ of several or a glTF mesh of several primitives, gives
        // a node several.
        var meshes = new List<(SceneMeshPayload Mesh, SceneMaterialPayload? Material)>();
        SceneCameraPayload? camera = null;
        SceneLightPayload? light = null;

        foreach (var c in node.Components)
        {
            switch (c)
            {
                case SceneMeshPayload m: meshes.Add((m, null)); break;
                case SceneMaterialPayload mat when meshes.Count > 0 && meshes[^1].Material is null: meshes[^1] = (meshes[^1].Mesh, mat); break;
                case SceneCameraPayload cam: camera ??= cam; break;
                case SceneLightPayload l: light ??= l; break;
            }
        }

        if (light is not null)
        {
            // Hand the payload off to ECS verbatim. LightSpawnSystem (Stage.PreUpdate, after
            // this system) turns it into a Light and removes the payload component.
            // The translation stays out of the scenes code, so scenes do not depend on
            // lighting, and lighting depends on scenes for the payload type alone.
            ecs.Add(entity, light);
        }

        // The first mesh is the node's own, and each after it a child at the node's place, since
        // an entity holds one mesh and one material.
        for (int i = 0; i < meshes.Count; i++)
        {
            var holder = entity;
            if (i > 0)
            {
                holder = ecs.Spawn();
                entities.Add(holder);
                ecs.Add(holder, DecomposeToTransform(Matrix4x4.Identity));
                ecs.Add(holder, new Name(node.Name));
                ecs.SetParent(holder, entity);
                if (settings.AttachSceneInstanceMarker)
                    ecs.Add(holder, new SceneInstance { SceneAssetId = sceneAssetId, SourcePath = node.SourcePath });
            }
            AttachMesh(ecs, holder, node, meshes[i].Mesh, meshes[i].Material, settings, ctx);
        }

        if (camera is not null)
        {
            // SceneCameraPayload holds physical aperture/focal length; the runtime
            // Camera takes radians directly. VerticalFovRadians does the math (or
            // returns 0 for orthographic, which the runtime camera doesn't model yet).
            float fovRad = camera.VerticalFovRadians;
            if (fovRad <= 0f) fovRad = Single.DegreesToRadians(60f); // ortho fallback
            ecs.Add(entity, new Camera
            {
                FovY = fovRad,
                Near = camera.NearClip,
                Far = camera.FarClip,
            });
        }
    }

    // Each mesh of a loaded scene de-indexed once, so every entity spawned from it shares its arrays
    // and the renderer, which keeps a mesh's buffers by its positions array, uploads it once. Made
    // again at each spawn, every cell of Manor that placed a model held buffers of its own, 176
    // meshes at sixteen cells and 259 at twenty-three, which climbed past the soak's bound as its
    // walk entered the house on a slow device. A scene loaded again is a new payload and is
    // de-indexed again, and the arrays go with the payload.
    private static readonly ConditionalWeakTable<SceneMeshPayload, Deindexed> s_deindexed = new();

    private sealed class Deindexed(Mesh mesh)
    {
        public readonly Mesh Mesh = mesh;
    }

    // A mesh and its material on an entity.
    private static void AttachMesh(EcsWorld ecs, int entity, SceneNode node, SceneMeshPayload mesh, SceneMaterialPayload? material,
        SceneSpawnSettings settings, SpawnContext ctx)
    {
        var shared = s_deindexed.GetValue(mesh, static mesh => new Deindexed(Deindex(mesh))).Mesh;
        ecs.Add(entity, shared);
        var positions = shared.Positions;

        // The material is the payload's when there is one and the configured default
        // otherwise, so the renderer sees a fully formed pair of mesh and material.
        var runtimeMaterial = BuildRuntimeMaterial(material, settings, ctx);
        ecs.Add(entity, runtimeMaterial);

        // Per-mesh diagnostic: vertex/tri count, source-space AABB and the final
        // world-space transform position the spawner produced. One pass over
        // the positions built above, which shows a wrong scale, a mesh off screen or
        // degenerate bounds without a debugger.
        LogMeshDiagnostics(node, entity, positions, runtimeMaterial.Albedo);

        // A spawn with no asset server leaves its textures out, which is said once rather than
        // for every material.
        if (material is not null && ctx.Server is null)
            WarnIfTexturesIgnoredOnce(material);
    }

    // De-indexed into three vertices per triangle, as Mesh holds them, with the normals and first
    // texture coordinates beside the positions when the file has them.
    private static Mesh Deindex(SceneMeshPayload mesh)
    {
        var count = mesh.Indices.Length;
        var positions = new Vector3[count];
        var normals = mesh.Normals is { } sourceNormals && sourceNormals.Length == mesh.Positions.Length ? new Vector3[count] : null;
        var uvs = mesh.Uv0 is { } sourceUvs && sourceUvs.Length == mesh.Positions.Length ? new Vector2[count] : null;
        for (int i = 0; i < count; i++)
        {
            var index = mesh.Indices[i];
            positions[i] = mesh.Positions[index];
            if (normals is not null) normals[i] = mesh.Normals![index];
            if (uvs is not null) uvs[i] = mesh.Uv0![index];
        }
        return new Mesh(positions, normals, uvs);
    }

    /// <summary>
    /// Builds a runtime <see cref="Material"/> from <paramref name="material"/>'s PBR
    /// factors and resolves any <see cref="SceneTextureRef"/>s into
    /// <see cref="Handle{T}"/>s via <paramref name="ctx"/>'s <see cref="AssetServer"/>.
    /// </summary>
    private static Material BuildRuntimeMaterial(SceneMaterialPayload? material, SceneSpawnSettings settings, SpawnContext ctx)
    {
        if (material is null)
            return new Material(settings.DefaultAlbedo);

        var runtime = new Material(material.BaseColorFactor)
        {
            MetallicFactor = material.MetallicFactor,
            RoughnessFactor = material.RoughnessFactor,
            EmissiveFactor = material.EmissiveFactor,
            NormalScale = material.NormalScale,
            OcclusionStrength = material.OcclusionStrength,
            AlphaMode = (MaterialAlphaMode)(byte)material.AlphaMode,
            AlphaCutoff = material.AlphaCutoff,
            DoubleSided = material.DoubleSided,
            SubsurfaceThickness = material.Thickness,
        };

        // Register the payload with the central MaterialLibrary so the renderer
        // can key per-material GPU resources (pipelines,
        // descriptor sets, uniform buffers) by a stable handle. Falls back to a
        // default handle when no library was supplied (test / legacy paths).
        if (ctx.Materials is not null)
            runtime.Handle = ctx.Materials.CreateOrGet(ToDescription(material));

        if (ctx.Server is null) return runtime;

        // sRGB textures: BaseColor + Emissive (per glTF / USD convention).
        // Linear textures: MetallicRoughness, Normal, Occlusion.
        runtime.BaseColorTexture           = LoadTexture(ctx, material.BaseColorTexture, srgb: true);
        runtime.EmissiveTexture            = LoadTexture(ctx, material.EmissiveTexture, srgb: true);
        runtime.MetallicRoughnessTexture   = LoadTexture(ctx, material.MetallicRoughnessTexture, srgb: false);
        runtime.NormalTexture              = LoadTexture(ctx, material.NormalTexture, srgb: false);
        runtime.OcclusionTexture           = LoadTexture(ctx, material.OcclusionTexture, srgb: false);
        return runtime;
    }

    /// <summary>
    /// Projects a <see cref="SceneMaterialPayload"/> onto the engine-neutral
    /// <see cref="MaterialDescription"/> shape consumed by <see cref="MaterialLibrary"/>.
    /// Texture references and the alpha-mode enum are translated 1:1; texture wrap
    /// modes share an enum order with <see cref="TextureWrapMode"/>.
    /// </summary>
    internal static MaterialDescription ToDescription(SceneMaterialPayload p) => new()
    {
        Name = p.Name,
        SourcePath = p.SourcePath,
        BaseColorFactor = p.BaseColorFactor,
        BaseColorTexture = ToMaterialTextureRef(p.BaseColorTexture),
        MetallicFactor = p.MetallicFactor,
        RoughnessFactor = p.RoughnessFactor,
        MetallicRoughnessTexture = ToMaterialTextureRef(p.MetallicRoughnessTexture),
        NormalTexture = ToMaterialTextureRef(p.NormalTexture),
        NormalScale = p.NormalScale,
        EmissiveFactor = p.EmissiveFactor,
        EmissiveTexture = ToMaterialTextureRef(p.EmissiveTexture),
        OcclusionTexture = ToMaterialTextureRef(p.OcclusionTexture),
        OcclusionStrength = p.OcclusionStrength,
        AlphaMode = (MaterialAlphaMode)(byte)p.AlphaMode,
        AlphaCutoff = p.AlphaCutoff,
        DoubleSided = p.DoubleSided,
    };

    private static MaterialTextureRef? ToMaterialTextureRef(SceneTextureRef? r) => r is null
        ? null
        : new MaterialTextureRef(r.AssetPath, r.UvSet, (TextureWrapMode)(byte)r.WrapS, (TextureWrapMode)(byte)r.WrapT);

    private static Handle<TextureAsset> LoadTexture(SpawnContext ctx, SceneTextureRef? texRef, bool srgb)
    {
        if (texRef is null || ctx.Server is null) return Handle<TextureAsset>.Invalid;
        var resolved = EmbeddedTexturePath(ctx, texRef.AssetPath) ?? ResolveTexturePath(ctx.SceneDirectory, texRef.AssetPath);
        if (string.IsNullOrEmpty(resolved)) return Handle<TextureAsset>.Invalid;

        var handle = srgb
            ? ctx.Server.LoadTextureSrgb(resolved, generateMips: true)
            : ctx.Server.LoadTextureLinear(resolved, generateMips: true);
        ctx.Taken.Add(handle.Id);
        return handle;
    }

    /// <summary>
    /// Publishes an image the scene file carries inside itself to the in-memory asset source and
    /// returns the path it is read from there, or returns null when the path names a file.
    /// </summary>
    /// <remarks>
    /// The path is keyed by the scene and the image's index, so two models whose materials both
    /// say <c>*0</c> do not share a texture. Raw pixels, which few formats store, have no file
    /// format for the texture loader to read and are left out with a warning.
    /// </remarks>
    private static string? EmbeddedTexturePath(SpawnContext ctx, string texturePath)
    {
        if (ctx.Scene.FindEmbeddedTexture(texturePath) is not { } embedded) return null;
        if (embedded.Encoded is not { } bytes)
        {
            Logger.Warn($"SceneSpawner: '{ctx.Scene.Name}' embeds {texturePath} as raw pixels, which the asset server cannot load. It is left out.");
            return string.Empty;
        }

        var index = ctx.Scene.EmbeddedTextures.IndexOf(embedded);
        var extension = embedded.FormatHint is { Length: > 0 } hint ? hint.ToLowerInvariant() : "png";
        var path = $"__embedded__/{ctx.SceneKey}/{index}.{extension}";
        InMemoryAssetReader.Publish(new AssetPath(path), bytes);
        return path;
    }

    /// <summary>
    /// Resolves a texture's <see cref="SceneTextureRef.AssetPath"/> against the directory
    /// of the source scene file. Already-rooted paths (no dot-segments, contains a slash
    /// at index 0, or starts with the scene directory) and synthetic in-memory paths
    /// (matching <c>__embedded__</c>) are returned verbatim.
    /// </summary>
    public static string ResolveTexturePath(string? sceneDirectory, string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath)) return string.Empty;

        var t = texturePath.Replace('\\', '/');
        // A rooted path or one into memory is kept as it is.
        if (t.StartsWith('/') || t.Contains("__embedded__/", StringComparison.Ordinal))
            return t.TrimStart('/');

        if (string.IsNullOrEmpty(sceneDirectory)) return t;

        var dir = sceneDirectory.Replace('\\', '/').TrimEnd('/');
        return string.IsNullOrEmpty(dir) ? t : $"{dir}/{t}";
    }

    private static Matrix4x4 ComposeLocalMatrix(in Transform t)
    {
        return Matrix4x4.CreateScale(t.Scale)
               * Matrix4x4.CreateFromQuaternion(t.Rotation)
               * Matrix4x4.CreateTranslation(t.Position);
    }

    private static Transform DecomposeToTransform(in Matrix4x4 m)
    {
        if (Matrix4x4.Decompose(m, out var scale, out var rotation, out var translation))
            return new Transform { Position = translation, Rotation = rotation, Scale = scale };

        // Degenerate (zero-scale axis or shear): fall back to translation-only so the
        // entity still ends up roughly in place rather than dropping silently.
        return new Transform
        {
            Position = new Vector3(m.M41, m.M42, m.M43),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One,
        };
    }

    private static ScenePurposeMask PurposeFlag(ScenePurpose p) => p switch
    {
        ScenePurpose.Render => ScenePurposeMask.Render,
        ScenePurpose.Proxy  => ScenePurposeMask.Proxy,
        ScenePurpose.Guide  => ScenePurposeMask.Guide,
        _                   => ScenePurposeMask.Default,
    };

    // Per-mesh diagnostic emitted by AttachComponents. Computes a quick local-space
    // AABB so anyone reading the log can spot the two top failure modes for "scene
    // loaded but nothing on screen": empty geometry, or geometry whose center/scale
    // puts it outside the camera frustum.
    private static void LogMeshDiagnostics(SceneNode node, int entity, Vector3[] positions, Vector4 albedo)
    {
        if (positions.Length == 0)
        {
            Logger.Warn($"SceneSpawner:   entity {entity} '{node.SourcePath}' has 0 vertices, so its mesh is not drawn.");
            return;
        }

        var min = positions[0];
        var max = positions[0];
        for (int i = 1; i < positions.Length; i++)
        {
            min = Vector3.Min(min, positions[i]);
            max = Vector3.Max(max, positions[i]);
        }
        var size = max - min;
        var center = (max + min) * 0.5f;

        Logger.Debug(
            $"SceneSpawner:   entity {entity} '{node.SourcePath}', " +
            $"verts={positions.Length}, tris={positions.Length / 3}, " +
            $"localAabb=[({min.X:0.##},{min.Y:0.##},{min.Z:0.##})..({max.X:0.##},{max.Y:0.##},{max.Z:0.##})] " +
            $"size=({size.X:0.##},{size.Y:0.##},{size.Z:0.##}) center=({center.X:0.##},{center.Y:0.##},{center.Z:0.##}), " +
            $"albedo=({albedo.X:0.##},{albedo.Y:0.##},{albedo.Z:0.##},{albedo.W:0.##}).");
    }

    // Whether a spawn with no asset server has said that it leaves textures out, once a process,
    // since a scene can have hundreds of textured materials.
    private static int s_textureWarningEmitted;

    private static void WarnIfTexturesIgnoredOnce(SceneMaterialPayload mat)
    {
        if (mat.BaseColorTexture is null
            && mat.MetallicRoughnessTexture is null
            && mat.NormalTexture is null
            && mat.EmissiveTexture is null
            && mat.OcclusionTexture is null) return;

        if (System.Threading.Interlocked.CompareExchange(ref s_textureWarningEmitted, 1, 0) != 0)
            return;

        Logger.Warn(
            $"SceneSpawner: material '{mat.SourcePath}' names textures, which a spawn with no asset server leaves out, " +
            "so its surfaces draw with their factors alone. This is said once a process.");
    }

    /// <summary>
    /// Test hook: resets the once-per-process "textures ignored" warning latch so unit
    /// tests can assert the dedupe behavior deterministically. Production code never
    /// calls this.
    /// </summary>
    internal static void ResetTextureWarningForTest()
        => System.Threading.Interlocked.Exchange(ref s_textureWarningEmitted, 0);

    /// <summary>Test hook: <c>true</c> once the dedupe latch has fired.</summary>
    internal static bool TextureWarningEmittedForTest
        => System.Threading.Volatile.Read(ref s_textureWarningEmitted) != 0;
}
