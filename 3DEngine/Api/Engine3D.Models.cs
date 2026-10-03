using System.Numerics;
using StbImageSharp;

namespace Engine;

/// <summary>An axis-aligned box, by its two opposite corners.</summary>
public readonly record struct BoundingBox(Vector3 Min, Vector3 Max)
{
    /// <summary>The box around every point in <paramref name="points"/>.</summary>
    public static BoundingBox Around(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty) return default;
        var (min, max) = (points[0], points[0]);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }
}

/// <summary>A mesh on the GPU, by its id in the <see cref="MeshStore"/>, with its size and bounds.</summary>
/// <remarks>A default mesh has id 0 and is not loaded. Drawing it draws nothing.</remarks>
public readonly record struct ModelMesh(int Id, int VertexCount, int TriangleCount, BoundingBox Bounds)
{
    /// <summary>Whether this names a mesh that was loaded.</summary>
    public bool IsValid => Id > 0;
}

/// <summary>What a mesh is drawn with: a color, multiplied with a texture when it has one.</summary>
public record struct ModelMaterial(Color Color, Texture2D Texture = default);

/// <summary>Meshes with their materials, loaded from a file or made from a mesh, drawn as one.</summary>
/// <remarks>
/// <para>
/// The fields are raylib's: <see cref="Meshes"/>, <see cref="Materials"/>, and
/// <see cref="MeshMaterial"/>, which gives the material index of each mesh. A program changes a
/// material by assigning to <see cref="Materials"/>, as in
/// <c>model.Materials[0].Texture = texture;</c>.
/// </para>
/// <para>
/// A file's node hierarchy is baked into the vertices when it loads, so every mesh is in the
/// model's own space and <see cref="Transform"/> is the only transform left.
/// </para>
/// </remarks>
public sealed class Model
{
    /// <summary>The model's meshes.</summary>
    public ModelMesh[] Meshes { get; init; } = [];

    /// <summary>The model's materials.</summary>
    public ModelMaterial[] Materials { get; init; } = [];

    /// <summary>For each mesh, the index of its material in <see cref="Materials"/>.</summary>
    public int[] MeshMaterial { get; init; } = [];

    /// <summary>Applied before the position, rotation and scale a draw call gives.</summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>Textures the model loaded itself, which <see cref="Engine3D.UnloadModel"/> frees.</summary>
    internal Texture2D[] OwnedTextures { get; init; } = [];

    /// <summary>Whether the model has a mesh to draw.</summary>
    public bool IsValid => Meshes.Length > 0;
}

public static partial class Engine3D
{
    private static MeshStore Meshes => World.Resource<MeshStore>();

    // -- Meshes

    /// <summary>Uploads vertices and triangle indices as a mesh.</summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, or one is out of range.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices)
    {
        var positions = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) positions[i] = vertices[i].Position;
        var id = Meshes.Add(vertices, indices);
        return new ModelMesh(id, vertices.Length, indices.Length / 3, BoundingBox.Around(positions));
    }

    /// <summary>Frees a mesh. Drawing it afterward draws nothing.</summary>
    public static void UnloadMesh(ModelMesh mesh)
    {
        if (mesh.IsValid) Meshes.Remove(mesh.Id);
    }

    /// <summary>Makes a box centered on the origin, with a normal and texture coordinates per face.</summary>
    public static ModelMesh GenMeshCube(float width, float height, float length)
    {
        var h = new Vector3(width, height, length) / 2;
        // Each face as its normal and two axes along it, so its corners go around counterclockwise
        // seen from outside.
        (Vector3 N, Vector3 U, Vector3 V)[] faces =
        [
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
        ];

        var vertices = new ModelVertex[24];
        var indices = new uint[36];
        for (int f = 0; f < 6; f++)
        {
            var (n, u, v) = faces[f];
            for (int c = 0; c < 4; c++)
            {
                var su = c is 1 or 2 ? 1f : -1f;
                var sv = c is 2 or 3 ? 1f : -1f;
                var p = (n + u * su + v * sv) * h;
                vertices[f * 4 + c] = new ModelVertex(p, n, new Vector2((su + 1) / 2, 1 - (sv + 1) / 2));
            }
            uint b = (uint)(f * 4);
            new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 }.CopyTo(indices, f * 6);
        }
        return UploadMesh(vertices, indices);
    }

    /// <summary>Makes a sphere centered on the origin.</summary>
    public static ModelMesh GenMeshSphere(float radius, int rings, int slices)
    {
        rings = Math.Max(2, rings);
        slices = Math.Max(3, slices);
        var vertices = new ModelVertex[(rings + 1) * (slices + 1)];
        for (int r = 0; r <= rings; r++)
        for (int s = 0; s <= slices; s++)
        {
            var polar = MathF.PI * r / rings;
            var azimuth = MathF.Tau * s / slices;
            var n = new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
            vertices[r * (slices + 1) + s] = new ModelVertex(n * radius, n, new Vector2((float)s / slices, (float)r / rings));
        }

        var indices = new List<uint>(rings * slices * 6);
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < slices; s++)
        {
            uint a = (uint)(r * (slices + 1) + s), b = a + (uint)(slices + 1);
            indices.AddRange([a, b, b + 1, a, b + 1, a + 1]);
        }
        return UploadMesh(vertices, [.. indices]);
    }

    /// <summary>Makes a flat rectangle on the XZ plane, centered on the origin, facing up.</summary>
    public static ModelMesh GenMeshPlane(float width, float length, int resX, int resZ)
    {
        resX = Math.Max(1, resX);
        resZ = Math.Max(1, resZ);
        var vertices = new ModelVertex[(resX + 1) * (resZ + 1)];
        for (int z = 0; z <= resZ; z++)
        for (int x = 0; x <= resX; x++)
        {
            var (u, v) = ((float)x / resX, (float)z / resZ);
            vertices[z * (resX + 1) + x] = new ModelVertex(new Vector3((u - 0.5f) * width, 0, (v - 0.5f) * length), Vector3.UnitY, new Vector2(u, v));
        }

        var indices = new List<uint>(resX * resZ * 6);
        for (int z = 0; z < resZ; z++)
        for (int x = 0; x < resX; x++)
        {
            uint a = (uint)(z * (resX + 1) + x), b = a + (uint)(resX + 1);
            indices.AddRange([a, b, b + 1, a, b + 1, a + 1]);
        }
        return UploadMesh(vertices, [.. indices]);
    }

    // -- Models

    /// <summary>
    /// Loads a model file through Assimp (glTF, FBX, OBJ, COLLADA and about forty more), with each
    /// material's base color and base color texture.
    /// </summary>
    /// <returns>The model, or an empty one when the file cannot be read, with the reason in the log.</returns>
    /// <remarks>
    /// Textures embedded in the file, as a <c>.glb</c> carries them, are decoded from it, and the
    /// rest are looked for beside the model file.
    /// </remarks>
    public static Model LoadModel(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadModel: '{fileName}' was not found beside the program or in the working directory.");
            return new Model();
        }

        Scene scene;
        try
        {
            scene = new AssimpModelReader().ReadFile(path, new SceneImportSettings());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Assimp.AssimpException)
        {
            ApiLogger.Warn($"LoadModel: '{fileName}' could not be read: {ex.Message}");
            return new Model();
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var materials = new List<ModelMaterial>();
        var materialIndex = new Dictionary<SceneMaterialPayload, int>();
        var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        var meshes = new List<ModelMesh>();
        var meshMaterial = new List<int>();

        void Visit(SceneNode node, Matrix4x4 parent)
        {
            var t = node.LocalTransform;
            var world = Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(t.Rotation)
                        * Matrix4x4.CreateTranslation(t.Position) * parent;

            // A node lists each of its meshes with that mesh's material, and the mesh names its
            // material by path in its subset.
            var nodeMaterials = node.Components.OfType<SceneMaterialPayload>()
                .GroupBy(m => m.SourcePath).ToDictionary(g => g.Key, g => g.First());

            foreach (var mesh in node.Components.OfType<SceneMeshPayload>())
            {
                var path = mesh.Subsets.FirstOrDefault()?.MaterialPath;
                meshes.Add(Bake(mesh, world));
                meshMaterial.Add(MaterialFor(path is not null ? nodeMaterials.GetValueOrDefault(path) : null));
            }

            foreach (var child in node.Children) Visit(child, world);
        }

        int MaterialFor(SceneMaterialPayload? material)
        {
            if (material is null)
            {
                materials.Add(new ModelMaterial(Color.White));
                return materials.Count - 1;
            }
            if (materialIndex.TryGetValue(material, out var index)) return index;

            var c = Vector4.Clamp(material.BaseColorFactor, Vector4.Zero, Vector4.One) * 255;
            var texture = default(Texture2D);
            if (material.BaseColorTexture is { AssetPath: var texturePath })
            {
                if (scene.FindEmbeddedTexture(texturePath) is { } embedded)
                {
                    var key = "embedded:" + texturePath;
                    if (!textures.TryGetValue(key, out texture))
                        textures[key] = texture = LoadEmbeddedTexture(embedded, fileName, texturePath);
                }
                else if (!texturePath.StartsWith('*'))
                {
                    var full = Path.Combine(directory, texturePath.Replace('\\', Path.DirectorySeparatorChar));
                    if (!textures.TryGetValue(full, out texture))
                        textures[full] = texture = LoadTexture(full);
                }
            }

            materials.Add(new ModelMaterial(new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)c.W), texture));
            return materialIndex[material] = materials.Count - 1;
        }

        foreach (var root in scene.Roots) Visit(root, Matrix4x4.Identity);

        if (meshes.Count == 0)
            ApiLogger.Warn($"LoadModel: '{fileName}' has no meshes.");

        return new Model
        {
            Meshes = [.. meshes],
            Materials = [.. materials],
            MeshMaterial = [.. meshMaterial],
            OwnedTextures = [.. textures.Values.Where(t => t.IsValid)],
        };
    }

    private static Texture2D LoadEmbeddedTexture(SceneEmbeddedTexture embedded, string fileName, string texturePath)
    {
        if (embedded.Rgba is { } rgba)
            return LoadTextureFromImage(new Image(rgba, embedded.Width, embedded.Height));

        try
        {
            var result = ImageResult.FromMemory(embedded.Encoded!, ColorComponents.RedGreenBlueAlpha);
            return LoadTextureFromImage(new Image(result.Data, result.Width, result.Height));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadModel: '{fileName}' embeds a texture ({texturePath}, {embedded.FormatHint}) that could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Makes a model of one mesh with a white material.</summary>
    public static Model LoadModelFromMesh(ModelMesh mesh) => new()
    {
        Meshes = [mesh],
        Materials = [new ModelMaterial(Color.White)],
        MeshMaterial = [0],
    };

    /// <summary>Frees a model's meshes and the textures it loaded. Textures assigned to it afterward are the program's to free.</summary>
    public static void UnloadModel(Model model)
    {
        foreach (var mesh in model.Meshes) UnloadMesh(mesh);
        foreach (var texture in model.OwnedTextures) UnloadTexture(texture);
    }

    /// <summary>Whether <paramref name="model"/> has meshes that are loaded.</summary>
    public static bool IsModelValid(Model model) => model.IsValid && model.Meshes.All(m => Meshes.Contains(m.Id));

    /// <summary>The box around every mesh of a model, in its own space before <see cref="Model.Transform"/>.</summary>
    public static BoundingBox GetModelBoundingBox(Model model)
    {
        if (!model.IsValid) return default;
        var corners = model.Meshes.SelectMany(m => new[] { m.Bounds.Min, m.Bounds.Max }).ToArray();
        return BoundingBox.Around(corners);
    }

    // -- Drawing models, through the camera BeginMode3D set

    /// <summary>Draws a model at a position, scaled the same on every axis, with its colors multiplied by <paramref name="tint"/>.</summary>
    public static void DrawModel(Model model, Vector3 position, float scale, Color tint) =>
        DrawModelEx(model, position, Vector3.UnitY, 0, new Vector3(scale), tint);

    /// <summary>Draws a model at a position, rotated by <paramref name="rotationAngle"/> degrees around <paramref name="rotationAxis"/>, and scaled.</summary>
    public static void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint)
    {
        var axis = rotationAxis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(rotationAxis);
        var world = model.Transform * Matrix4x4.CreateScale(scale)
                    * Matrix4x4.CreateFromAxisAngle(axis, float.DegreesToRadians(rotationAngle))
                    * Matrix4x4.CreateTranslation(position);

        for (int i = 0; i < model.Meshes.Length; i++)
        {
            var material = model.Materials.Length == 0 ? new ModelMaterial(Color.White)
                : model.Materials[Math.Clamp(model.MeshMaterial.ElementAtOrDefault(i), 0, model.Materials.Length - 1)];
            DrawMesh(model.Meshes[i], material with { Color = Multiply(material.Color, tint) }, world);
        }
    }

    /// <summary>Draws a model's triangle edges in one color, at a position and scaled the same on every axis.</summary>
    public static void DrawModelWires(Model model, Vector3 position, float scale, Color tint) =>
        DrawModelWiresEx(model, position, Vector3.UnitY, 0, new Vector3(scale), tint);

    /// <summary>Draws a model's triangle edges in one color, rotated (degrees) and scaled.</summary>
    /// <remarks>
    /// The edges are lines in the immediate pass, one per edge however many triangles share it,
    /// built each call from the mesh's arrays, so a dense model costs its edge count every frame.
    /// </remarks>
    public static void DrawModelWiresEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint)
    {
        var axis = rotationAxis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(rotationAxis);
        var world = model.Transform * Matrix4x4.CreateScale(scale)
                    * Matrix4x4.CreateFromAxisAngle(axis, float.DegreesToRadians(rotationAngle))
                    * Matrix4x4.CreateTranslation(position);

        var edges = new HashSet<(uint, uint)>();
        foreach (var mesh in model.Meshes)
        {
            if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices)) continue;
            edges.Clear();
            for (int i = 0; i < indices.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    uint a = indices[i + k], b = indices[i + (k + 1) % 3];
                    if (edges.Add(a < b ? (a, b) : (b, a)))
                        DrawLine3D(Vector3.Transform(vertices[a].Position, world), Vector3.Transform(vertices[b].Position, world), tint);
                }
        }
    }

    /// <summary>Draws one mesh with a material and a model to world transform.</summary>
    public static void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform)
    {
        if (!mesh.IsValid) return;
        var texture = material.Texture.IsValid ? material.Texture.Id : 0;
        World.Resource<ModelDrawList>().Add(new ModelDraw(mesh.Id, transform, DrawList.Transform, material.Color, texture, DrawList.Target));
    }

    /// <summary>Draws a box's edges.</summary>
    public static void DrawBoundingBox(BoundingBox box, Color color) =>
        DrawCubeWiresV((box.Min + box.Max) / 2, box.Max - box.Min, color);

    private static Color Multiply(Color a, Color b) =>
        new((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255), (byte)(a.A * b.A / 255));

    // Moves a file's mesh into the model's space by its node's transform, so the model needs no
    // hierarchy at draw time.
    private static ModelMesh Bake(SceneMeshPayload mesh, Matrix4x4 world)
    {
        var vertices = new ModelVertex[mesh.Positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var normal = mesh.Normals is { } normals && i < normals.Length ? Vector3.TransformNormal(normals[i], world) : Vector3.UnitY;
            var uv = mesh.Uv0 is { } uvs && i < uvs.Length ? uvs[i] : Vector2.Zero;
            vertices[i] = new ModelVertex(Vector3.Transform(mesh.Positions[i], world),
                normal == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(normal), uv);
        }

        var indices = new uint[mesh.Indices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = (uint)mesh.Indices[i];
        return UploadMesh(vertices, indices);
    }
}
