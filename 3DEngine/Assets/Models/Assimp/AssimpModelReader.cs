using System.Numerics;
using A = Assimp;

namespace Engine;

/// <summary>
/// <see cref="ISceneReader"/> backed by AssimpNetter. Imports any of the ~40 file
/// formats the native Assimp library recognizes (FBX, OBJ, COLLADA, 3DS, BLEND, PLY,
/// STL, X, MD2/3/5, IFC, ...) and emits a backend-agnostic <see cref="Scene"/> snapshot
/// in the payloads every reader uses, so the spawn systems after it do not depend on the
/// importer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate / unit policy:</b> the reader
/// records <see cref="Scene.SourceCoordinateSystem"/> + <see cref="Scene.SourceMetersPerUnit"/>
/// from the source metadata where available (<c>aiMetadata</c> "UpAxis" / "UnitScaleFactor"
/// for FBX, <c>up_axis</c> for COLLADA). It does <i>not</i> rotate or rescale vertex data;
/// spawn systems apply a single root-level basis-change matrix.
/// </para>
/// <para>
/// <b>Files:</b> Assimp opens nothing itself. Every file it asks for, the model and those beside
/// it (an OBJ's <c>.mtl</c>, a glTF's <c>.bin</c>), is a C# stream that <see cref="AssimpFiles"/>
/// hands it, from the folder of a path or from the asset reader the model came from, so no native
/// handle is open for a child process to inherit and a model in any reader has its siblings.
/// </para>
/// <para>
/// <b>Coverage (v1):</b> meshes (triangulated, normals, tangents, primary + secondary
/// UVs, vertex colors, joint indices/weights), per-mesh material binding, basic PBR
/// material factors mapped from Assimp's <c>aiMaterial</c> property bag (diffuse,
/// metallic, roughness, normal, emissive and occlusion, as
/// <see cref="SceneMaterialPayload"/> holds them). Skeletons are extracted from
/// <see cref="A.Mesh.Bones"/> into <see cref="SceneSkeletonPayload"/>; animations
/// (<see cref="A.Scene.Animations"/>) are sampled into <see cref="SceneAnimationPayload"/>.
/// Cameras (<see cref="A.Scene.Cameras"/>) and lights (<see cref="A.Scene.Lights"/>) are
/// translated to <see cref="SceneCameraPayload"/> / <see cref="SceneLightPayload"/>.
/// </para>
/// <para>
/// <b>Trimming:</b> a game published trimmed or native is told that the AssimpNetter assembly
/// produced trim and AOT analysis warnings (IL2104, IL3053). They are the package's own, from
/// code inside it that binds Assimp's native functions to delegates and marshals its structures
/// by reflection, and this reader is where the engine calls it. A native game reads its models through it as the workflow's native run shows
/// (<c>build/play-native.sh</c>), and the warnings stand where the publish prints them.
/// </para>
/// </remarks>
internal sealed partial class AssimpModelReader : ISceneReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Models.Assimp");

    /// <inheritdoc />
    public string[] Extensions => _extensions ??= ResolveExtensions();
    private string[]? _extensions;

    /// <inheritdoc />
    public string FormatId => "assimp";

    /// <summary>
    /// Imports a model from a file on disk, with the files it names beside it (an OBJ's
    /// <c>.mtl</c>, a glTF's <c>.bin</c>) read from its folder. The flat API's <c>LoadModel</c>
    /// has the path and uses this.
    /// </summary>
    internal Scene ReadFile(string path, SceneImportSettings settings, CancellationToken ct = default)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? "";
        var name = System.IO.Path.GetFileName(path);
        using var context = new AssetLoadContext(Stream.Null, new AssetPath(name), _ => default);
        // Assimp is given the file's name alone, and the folder is added here, so a path with
        // letters outside ASCII never passes through native code as a string.
        return Import(name, new AssimpFiles(file => AssimpFiles.OnDisk(directory, file)), context, settings, ct);
    }

    private static Scene Import(string path, AssimpFiles files, AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        using var _ = files;
        using var importer = new A.AssimpContext();
        importer.SetIOSystem(files);
        // A joint that weighs no vertex, as a socket a sword hangs from, is kept among the bones,
        // as raylib keeps every joint of a glTF skin, where Assimp would leave it out.
        importer.SetConfig(new A.Configs.RemoveEmptyBonesConfig(false));

        const A.PostProcessSteps Steps =
            A.PostProcessSteps.Triangulate
            | A.PostProcessSteps.GenerateSmoothNormals
            | A.PostProcessSteps.CalculateTangentSpace
            | A.PostProcessSteps.JoinIdenticalVertices
            | A.PostProcessSteps.ImproveCacheLocality
            | A.PostProcessSteps.LimitBoneWeights      // clamp to 4 influences/vertex
            | A.PostProcessSteps.GenerateUVCoords
            | A.PostProcessSteps.SortByPrimitiveType
            | A.PostProcessSteps.RemoveRedundantMaterials
            | A.PostProcessSteps.FindInvalidData
            // Assimp counts V up from an image's bottom, as OBJ does, and turns glTF's over to
            // match. The engine samples V down from the top row, as glTF and raylib count it.
            | A.PostProcessSteps.FlipUVs;

        // A file the reader failed to give is the reason, ahead of what Assimp made of its absence.
        A.Scene? aScene;
        try
        {
            aScene = importer.ImportFile(path, Steps);
        }
        catch (A.AssimpException)
        {
            files.ThrowIfFailed();
            throw;
        }
        files.ThrowIfFailed();
        if (aScene is null || aScene.RootNode is null)
            throw new InvalidOperationException($"AssimpModelReader: ImportFile returned null for '{context.Path}'.");

        return BuildScene(aScene, context, settings, ct);
    }

    public Task<Scene> ReadAsync(AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        // Assimp opens a model more than once, first to tell its format and then to read it. A
        // stream that can seek is read where it is, each open at a place of its own in it, and one
        // that cannot is read into memory once. The files beside it come from the reader it came
        // from, so a model under any reader has its .mtl and .bin.
        var stream = context.GetStream();
        var shared = stream.CanSeek ? stream : InMemory(stream);
        try
        {
            var files = new AssimpFiles(file => Seekable(context.OpenFromSource(new AssetPath(file))))
            {
                SharedName = context.Path.Path,
                Shared = shared,
            };
            return Task.FromResult(Import(context.Path.Path, files, context, settings, ct));
        }
        finally
        {
            if (shared != stream) shared.Dispose();
        }
    }

    private static MemoryStream InMemory(Stream stream)
    {
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    // Assimp seeks in what it reads, which a reader's stream need not allow.
    private static Stream? Seekable(Stream? stream)
    {
        if (stream is null || stream.CanSeek) return stream;
        using (stream) return InMemory(stream);
    }

    // -- aiScene → Scene

    private static Scene BuildScene(A.Scene aScene, AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        var (upAxis, mpu) = ReadSourceBasis(aScene);

        var scene = new Scene
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(context.Path.Path),
            SourceCoordinateSystem = upAxis,
            SourceMetersPerUnit = mpu,
        };

        // Pre-pass: convert materials so each mesh-binding looks up the same shared payload.
        // An OBJ's dissolve (d) is left unread, as raylib's loader leaves it, so a file whose
        // material says d 0, as raylib's character.obj does, draws as it does in raylib.
        var readsOpacity = !string.Equals(System.IO.Path.GetExtension(context.Path.Path), ".obj", StringComparison.OrdinalIgnoreCase);
        var materials = settings.LoadPayloads.HasFlag(LoadPayloads.Materials)
            ? BuildMaterials(aScene, readsOpacity)
            : Array.Empty<SceneMaterialPayload>();

        // The meshes are converted once, first, since aiNode.MeshIndices names them by index.
        var meshes = settings.LoadPayloads.HasFlag(LoadPayloads.Meshes)
            ? BuildMeshes(aScene, materials, ct)
            : Array.Empty<SceneMeshPayload>();

        // Pre-pass: skeletons keyed by source-path of the bone-root node, so a SceneSkinPayload
        // can reference its skeleton by SourcePath without a second pass.
        var skeletons = settings.LoadPayloads.HasFlag(LoadPayloads.Meshes) // skinning is mesh-adjacent
            ? BuildSkeletons(aScene)
            : new Dictionary<string, SceneSkeletonPayload>(StringComparer.Ordinal);

        // Convert nodes recursively.
        var rootNode = ConvertNode(aScene.RootNode, "/", aScene, meshes, materials, skeletons, settings, ct);
        if (rootNode is not null)
        {
            // Assimp wraps everything in a synthetic root ("RootNode" by default). Promote
            // its children to scene roots when the root itself carries no payload, mirroring
            // the conventional handling for FBX / glTF imports.
            if (rootNode.Components.Count == 0 && rootNode.LocalTransform.Position == Vector3.Zero
                && rootNode.LocalTransform.Rotation == Quaternion.Identity
                && rootNode.LocalTransform.Scale == Vector3.One)
            {
                foreach (var child in rootNode.Children) scene.Roots.Add(child);
            }
            else
            {
                scene.Roots.Add(rootNode);
            }
        }

        // Every embedded image is kept, in Assimp's order, because materials name them by index.
        if (aScene.HasTextures)
            foreach (var texture in aScene.Textures)
                scene.EmbeddedTextures.Add(ConvertEmbedded(texture));

        // Animations, a clip per aiAnimation, attached to the scene root, whose channels name
        // the nodes they move. LoadPayloads has no flag of their own, so they are read when the
        // source authored any clips.
        if (aScene.AnimationCount > 0)
        {
            // The clips ride the first root, where Scene.Traverse finds them.
            if (scene.Roots.Count == 0)
            {
                scene.Roots.Add(new SceneNode { Name = "Root", SourcePath = "/" });
            }
            foreach (var anim in aScene.Animations)
            {
                ct.ThrowIfCancellationRequested();
                var clip = ConvertAnimation(anim);
                if (clip is not null) scene.Roots[0].Components.Add(clip);
            }
        }

        LogSummary(context, scene, materials.Length, meshes.Length, skeletons.Count, aScene.AnimationCount);
        return scene;
    }

    // -- Source basis (FBX UpAxis / UnitScaleFactor; COLLADA up_axis)

    private static (SceneCoordinateSystem upAxis, double metersPerUnit) ReadSourceBasis(A.Scene aScene)
    {
        var upAxis = SceneCoordinateSystem.YUp;
        var mpu = 1.0;
        var meta = aScene.Metadata;
        if (meta is null || meta.Count == 0) return (upAxis, mpu);

        if (meta.TryGetValue("UpAxis", out var upEntry) && upEntry.Data is int upInt)
        {
            // FBX convention: 0 = X, 1 = Y, 2 = Z.
            if (upInt == 2) upAxis = SceneCoordinateSystem.ZUp;
        }
        if (meta.TryGetValue("UnitScaleFactor", out var unitEntry) && unitEntry.Data is double unitDouble)
        {
            // FBX authors UnitScaleFactor in centimeters; convert to meters per unit.
            mpu = unitDouble / 100.0;
        }
        return (upAxis, mpu);
    }

    // -- Nodes

    private static SceneNode? ConvertNode(
        A.Node aNode,
        string parentPath,
        A.Scene aScene,
        SceneMeshPayload[] meshes,
        SceneMaterialPayload[] materials,
        IReadOnlyDictionary<string, SceneSkeletonPayload> skeletons,
        SceneImportSettings settings,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (aNode is null) return null;

        var name = string.IsNullOrEmpty(aNode.Name) ? "Node" : aNode.Name;
        var path = parentPath.EndsWith('/') ? parentPath + name : parentPath + "/" + name;

        var node = new SceneNode
        {
            Name = name,
            SourcePath = path,
            LocalTransform = DecomposeMatrix(FromAssimp(aNode.Transform)),
            Purpose = ScenePurpose.Default,
            Enabled = true,
        };

        // Attach all meshes referenced by this aiNode (an aiNode typically references
        // exactly one mesh after SortByPrimitiveType, but multi-material legacy formats
        // can reference several).
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Meshes) && aNode.HasMeshes)
        {
            foreach (int meshIndex in aNode.MeshIndices)
            {
                if (meshIndex < 0 || meshIndex >= meshes.Length) continue;
                var mesh = meshes[meshIndex];
                node.Components.Add(mesh);

                // Also attach the bound material payload(s) so spawners see them on the same node.
                if (settings.LoadPayloads.HasFlag(LoadPayloads.Materials))
                {
                    var am = aScene.Meshes[meshIndex];
                    if (am.MaterialIndex >= 0 && am.MaterialIndex < materials.Length)
                        node.Components.Add(materials[am.MaterialIndex]);
                }

                // Skinning: attach the SceneSkinPayload alongside the mesh.
                var am2 = aScene.Meshes[meshIndex];
                var skin = BuildSkin(am2, meshIndex, skeletons);
                if (skin is not null)
                {
                    node.Components.Add(skin);
                    if (skeletons.TryGetValue(skin.SkeletonPath, out var skel))
                        node.Components.Add(skel);
                }
            }
        }

        // Camera / light: aiCamera/aiLight reference an aiNode by name.
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Cameras))
        {
            for (int i = 0; i < aScene.CameraCount; i++)
            {
                if (aScene.Cameras[i].Name == name)
                {
                    node.Components.Add(ConvertCamera(aScene.Cameras[i]));
                    break;
                }
            }
        }
        if (settings.LoadPayloads.HasFlag(LoadPayloads.Lights))
        {
            for (int i = 0; i < aScene.LightCount; i++)
            {
                if (aScene.Lights[i].Name == name)
                {
                    node.Components.Add(ConvertLight(aScene.Lights[i]));
                    break;
                }
            }
        }

        if (aNode.HasChildren)
        {
            foreach (var child in aNode.Children)
            {
                var childNode = ConvertNode(child, path, aScene, meshes, materials, skeletons, settings, ct);
                if (childNode is not null) node.Children.Add(childNode);
            }
        }

        return node;
    }

    // -- Cameras / lights

    private static SceneCameraPayload ConvertCamera(A.Camera c)
    {
        // Assimp camera units: HorizontalFOV in radians; aspect; clip planes.
        // Convert to a synthesised aperture / focal length so the payload round-trips
        // through the existing UsdPreviewSurface-shaped fields.
        const float ReferenceVertAperture = 15.2908f; // 35mm Academy
        float hfov = c.FieldOfview > 0f ? c.FieldOfview : MathF.PI / 3f;
        float aspect = c.AspectRatio > 0f ? c.AspectRatio : 16f / 9f;
        float vfov = 2f * MathF.Atan(MathF.Tan(hfov * 0.5f) / aspect);
        float focalLength = ReferenceVertAperture / (2f * MathF.Tan(vfov * 0.5f));
        return new SceneCameraPayload
        {
            Name = string.IsNullOrEmpty(c.Name) ? "Camera" : c.Name,
            Projection = SceneProjection.Perspective,
            HorizontalAperture = ReferenceVertAperture * aspect,
            VerticalAperture = ReferenceVertAperture,
            FocalLength = focalLength,
            NearClip = c.ClipPlaneNear,
            FarClip = c.ClipPlaneFar,
        };
    }

    private static SceneLightPayload ConvertLight(A.Light l)
    {
        var spot = l.LightType == A.LightSourceType.Spot;
        return new SceneLightPayload
        {
            Name = string.IsNullOrEmpty(l.Name) ? "Light" : l.Name,
            // An area light has no counterpart in the model pass, so it lights as a point.
            Kind = l.LightType switch
            {
                A.LightSourceType.Directional => LightKind.Directional,
                A.LightSourceType.Spot => LightKind.Spot,
                A.LightSourceType.Ambient => LightKind.Ambient,
                _ => LightKind.Point,
            },
            Color = l.ColorDiffuse,
            Intensity = 1f,
            // Assimp gives a spot's cone angles from its axis in radians, as glTF writes them.
            InnerAngle = spot ? float.RadiansToDegrees(l.AngleInnerCone) : 25f,
            OuterAngle = spot ? float.RadiansToDegrees(l.AngleOuterCone) : 30f,
        };
    }

    // -- Math helpers

    // AssimpNetter hands over Assimp's matrices as they are, which act on column vectors with the
    // translation in the last column. System.Numerics acts on row vectors with it in the last row,
    // so each is transposed once here. Read as they came, every node's translation was zero.
    private static Matrix4x4 FromAssimp(Matrix4x4 m) => Matrix4x4.Transpose(m);

    private static Transform DecomposeMatrix(Matrix4x4 m)
    {
        if (!Matrix4x4.Decompose(m, out var scale, out var rotation, out var translation))
        {
            translation = m.Translation;
            rotation = Quaternion.Identity;
            scale = Vector3.One;
        }
        return new Transform { Position = translation, Rotation = rotation, Scale = scale };
    }

    private static string[] ResolveExtensions()
    {
        try
        {
            using var ctx = new A.AssimpContext();
            var raw = ctx.GetSupportedImportFormats();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                var e = r.TrimStart('*');
                if (!e.StartsWith('.')) e = "." + e;
                if (e is ".usd" or ".usda" or ".usdc" or ".usdz") continue;
                set.Add(e);
            }
            return set.ToArray();
        }
        catch
        {
            return new[] { ".fbx", ".obj", ".dae", ".3ds", ".blend", ".ply", ".stl", ".x" };
        }
    }

    // -- Logging

    private static void LogSummary(AssetLoadContext context, Scene scene, int mats, int meshes, int skels, int anims)
    {
        int nodes = 0, attached = 0;
        foreach (var r in scene.Roots) Tally(r);

        Logger.Info(
            $"AssimpModelReader: '{context.Path}' parsed, upAxis={scene.SourceCoordinateSystem}, " +
            $"mpu={scene.SourceMetersPerUnit:0.###}, roots={scene.Roots.Count}, nodes={nodes}, " +
            $"meshes={meshes}, materials={mats}, skeletons={skels}, animations={anims}, payloads={attached}.");

        void Tally(SceneNode n)
        {
            nodes++;
            attached += n.Components.Count;
            foreach (var c in n.Children) Tally(c);
        }
    }
}