using System.Numerics;

namespace Engine;

/// <summary>What a mesh needs to be posed, which is its vertices at rest and the bones that move each.</summary>
/// <param name="Mesh">The index of the mesh in <see cref="Model.Meshes"/>.</param>
/// <param name="Rest">The mesh's vertices in the model's space, as loaded.</param>
/// <param name="Joints">Four joint indices per vertex, into <paramref name="BoneOfJoint"/>.</param>
/// <param name="Weights">Four weights per vertex, summing to one.</param>
/// <param name="BoneOfJoint">The model bone each of the mesh's joints is.</param>
/// <param name="FromRest">For each joint, model space at rest to the joint's own space.</param>
internal sealed record SkinnedMesh(int Mesh, ModelVertex[] Rest, ushort[] Joints, float[] Weights, int[] BoneOfJoint, Matrix4x4[] FromRest)
{
    /// <summary>The name of the node the mesh hangs from, which a clip's morph weights name.</summary>
    public string Node { get; init; } = "";

    /// <summary>The mesh's morph targets' names, empty for a mesh with none.</summary>
    public string[] MorphNames { get; init; } = [];

    /// <summary>Each morph target's weight now, which clips and <see cref="Engine3D.SetModelMorphWeight"/> change in place.</summary>
    public float[] MorphWeights { get; init; } = [];

    /// <summary>How far each target moves each vertex at full weight, in the model's space.</summary>
    public Vector3[][] MorphPositions { get; init; } = [];

    /// <summary>How each target turns each vertex's normal at full weight, or null for a target with none.</summary>
    public Vector3[]?[] MorphNormals { get; init; } = [];
}

public static partial class Engine3D
{
    /// <summary>How many frames a second <see cref="LoadModelAnimations"/> samples a clip at.</summary>
    public const int AnimationFps = 60;

    /// <summary>
    /// Loads every clip of a model file (glTF, FBX, COLLADA and the rest Assimp reads), each
    /// sampled into poses at <see cref="AnimationFps"/> frames a second.
    /// </summary>
    /// <returns>The clips, or none when the file cannot be read or has none, with the reason in the log.</returns>
    /// <remarks>
    /// An Inter-Quake Model's clips keep the file's own frames, as raylib reads them, whatever rate
    /// the file gives. A file of clips alone names no bones, so its clips fit a model of as many
    /// bones under the same parents (<see cref="IsModelAnimationValid"/>). A Model 3D file's actions
    /// are posed every 17 milliseconds, as raylib samples them, with raylib's last bone that never
    /// moves.
    /// </remarks>
    public static ModelAnimation[] LoadModelAnimations(string fileName)
    {
        switch (Path.GetExtension(fileName).ToLowerInvariant())
        {
            case ".iqm": return ReadModelFile(fileName, "LoadModelAnimations", (data, _) => IqmModelReader.ReadAnimations(data)) ?? [];
            case ".m3d": return ReadModelFile(fileName, "LoadModelAnimations", (data, _) => M3dModelReader.ReadAnimations(data)) ?? [];
        }
        if (ReadModelScene(fileName, "LoadModelAnimations") is not { } scene) return [];

        var bones = ModelSkeleton.Bones(scene);
        var nodes = ModelSkeleton.NodesByName(scene);
        var clips = new List<ModelAnimation>();
        foreach (var node in ModelSkeleton.Walk(scene))
            foreach (var clip in node.Components.OfType<SceneAnimationPayload>())
                clips.Add(ModelSkeleton.Sample(scene, clip, bones, nodes));

        if (clips.Count == 0) ApiLogger.Warn($"LoadModelAnimations: '{fileName}' has no animations.");
        return [.. clips];
    }

    /// <summary>
    /// Poses <paramref name="model"/> as <paramref name="animation"/> has it at <paramref name="frame"/>,
    /// counted round the clip's length, by moving each skinned mesh's vertices from their rest. A
    /// frame between two is a blend of them, as raylib's is, so a clip played at half speed moves
    /// smoothly.
    /// </summary>
    /// <remarks>
    /// The GPU poses the vertices, handed only the joints' matrices, in the frame drawn next, and
    /// the mesh's own vertices stay at rest. With no renderer the CPU poses them and they replace
    /// the mesh's, as raylib does it, at a cost of the mesh's vertex count every call. A model with
    /// no bones, or a clip whose bones are not the model's (<see cref="IsModelAnimationValid"/>),
    /// is left as it is.
    /// </remarks>
    public static void UpdateModelAnimation(Model model, ModelAnimation animation, float frame)
    {
        if (animation.FrameCount == 0 || !IsModelAnimationValid(model, animation)) return;
        if (frame != MathF.Floor(frame))
        {
            // Between two frames, which the clip is sampled at AnimationFps apart
            UpdateModelAnimationAt(model, animation, frame / AnimationFps);
            return;
        }
        var whole = (int)frame;
        whole = ((whole % animation.FrameCount) + animation.FrameCount) % animation.FrameCount;
        ApplyMorphs(model, animation, animation.FrameMorphWeights.Length > whole ? animation.FrameMorphWeights[whole] : null, 1);
        Pose(model, animation.FramePoses[whole]);
    }

    /// <summary>
    /// Poses <paramref name="model"/> as <paramref name="animation"/> has it <paramref name="seconds"/>
    /// into the clip, counted round its length, between the two frames either side.
    /// </summary>
    /// <remarks>
    /// Each bone's position and scale are interpolated in a straight line and its rotation along the
    /// sphere, so a clip played at any frame rate moves smoothly where whole frames would step.
    /// </remarks>
    public static void UpdateModelAnimationAt(Model model, ModelAnimation animation, float seconds)
    {
        if (animation.FrameCount == 0 || !IsModelAnimationValid(model, animation)) return;
        ApplyMorphs(model, animation, SampleMorphs(animation, seconds), 1);
        Pose(model, Sample(animation, seconds));
    }

    /// <summary>
    /// Poses <paramref name="model"/> between two clips at frames of each, <paramref name="blend"/> of
    /// the way from the first to the second, as raylib's does by frame where
    /// <see cref="UpdateModelAnimationBlend"/> takes seconds.
    /// </summary>
    public static void UpdateModelAnimationEx(Model model, ModelAnimation animA, float frameA, ModelAnimation animB, float frameB, float blend) =>
        UpdateModelAnimationBlend(model, animA, frameA / AnimationFps, animB, frameB / AnimationFps, blend);

    /// <summary>
    /// Poses <paramref name="model"/> between two clips, <paramref name="from"/> at
    /// <paramref name="fromSeconds"/> and <paramref name="to"/> at <paramref name="toSeconds"/>,
    /// <paramref name="weight"/> of the way from the first to the second, as a walk turning into a run.
    /// </summary>
    /// <remarks>Both clips move the model's bones, and each is sampled as <see cref="UpdateModelAnimationAt"/> does.</remarks>
    public static void UpdateModelAnimationBlend(Model model, ModelAnimation from, float fromSeconds, ModelAnimation to, float toSeconds, float weight)
    {
        if (from.FrameCount == 0 || to.FrameCount == 0 || !IsModelAnimationValid(model, from) || !IsModelAnimationValid(model, to)) return;
        weight = Math.Clamp(weight, 0f, 1f);
        ApplyMorphs(model, from, SampleMorphs(from, fromSeconds), 1);
        ApplyMorphs(model, to, SampleMorphs(to, toSeconds), weight);
        Pose(model, Mix(Sample(from, fromSeconds), Sample(to, toSeconds), weight));
    }

    /// <summary>
    /// Poses <paramref name="model"/> by <paramref name="under"/> at <paramref name="underSeconds"/>,
    /// with <paramref name="over"/> at <paramref name="overSeconds"/> playing on
    /// <paramref name="bone"/> and every bone below it, <paramref name="weight"/> of the way from the
    /// first clip to the second there, as a wave of the arm over a run, or a head turning while the
    /// rest walks.
    /// </summary>
    /// <remarks>
    /// The bones below <paramref name="bone"/> take the second clip's pose relative to their parents,
    /// so the arm waves from wherever the running body carries its shoulder. The morph targets the
    /// second clip moves are moved <paramref name="weight"/> of the way to its weights, as a face's
    /// expression played over a walk, and the rest keep the first clip's. A bone the model does not
    /// have poses the model by the first clip alone.
    /// </remarks>
    public static void UpdateModelAnimationLayer(Model model, ModelAnimation under, float underSeconds, ModelAnimation over, float overSeconds,
        string bone, float weight = 1)
    {
        if (under.FrameCount == 0 || over.FrameCount == 0 || !IsModelAnimationValid(model, under) || !IsModelAnimationValid(model, over)) return;
        var root = Array.FindIndex(under.Bones, b => b.Name == bone);
        var below = Sample(under, underSeconds);
        ApplyMorphs(model, under, SampleMorphs(under, underSeconds), 1);
        if (root >= 0) ApplyMorphs(model, over, SampleMorphs(over, overSeconds), Math.Clamp(weight, 0f, 1f));
        Pose(model, root < 0 ? below : Layer(under.Bones, below, Sample(over, overSeconds), root, Math.Clamp(weight, 0f, 1f)));
    }

    // The bones from root down posed by the second clip relative to their parents, weight of the way
    // from the first clip's, and composed onto where the first clip puts what they hang from.
    private static Transform[] Layer(BoneInfo[] bones, Transform[] under, Transform[] over, int root, float weight)
    {
        var inLayer = new bool[bones.Length];
        for (int b = 0; b < bones.Length; b++)
            for (var at = b; at >= 0 && !inLayer[b]; at = bones[at].Parent)
                inLayer[b] = at == root;

        var result = (Transform[])under.Clone();
        var done = new bool[bones.Length];
        Matrix4x4 Placed(int b)
        {
            if (!inLayer[b]) return TransformPropagation.ToMatrix(under[b]);
            if (done[b]) return TransformPropagation.ToMatrix(result[b]);
            var parent = bones[b].Parent;
            var local = Mix([Relative(under, bones, b)], [Relative(over, bones, b)], weight)[0];
            var placed = TransformPropagation.ToMatrix(local) * (parent >= 0 ? Placed(parent) : Matrix4x4.Identity);
            if (!Matrix4x4.Decompose(placed, out var scale, out var rotation, out var position))
                (scale, rotation, position) = (Vector3.One, Quaternion.Identity, placed.Translation);
            result[b] = new Transform { Position = position, Rotation = rotation, Scale = scale };
            done[b] = true;
            return placed;
        }
        for (int b = 0; b < bones.Length; b++) Placed(b);
        return result;
    }

    // A bone's pose relative to its parent's, from the model-space poses a clip keeps.
    private static Transform Relative(Transform[] poses, BoneInfo[] bones, int b)
    {
        var matrix = TransformPropagation.ToMatrix(poses[b]);
        if (bones[b].Parent >= 0 && Matrix4x4.Invert(TransformPropagation.ToMatrix(poses[bones[b].Parent]), out var inverse)) matrix *= inverse;
        if (!Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var position))
            (scale, rotation, position) = (Vector3.One, Quaternion.Identity, matrix.Translation);
        return new Transform { Position = position, Rotation = rotation, Scale = scale };
    }

    /// <summary>
    /// Sets the weight of every morph target named <paramref name="target"/> in the model's meshes,
    /// 0 for its shape at rest and 1 for the target's, as a face's smile or a ball's squash, and
    /// poses the model again as its bones were last posed.
    /// </summary>
    /// <remarks>A clip that moves the same target's weight sets it again when it is played.</remarks>
    public static void SetModelMorphWeight(Model model, string target, float weight)
    {
        var found = false;
        foreach (var skin in model.Skins)
            for (int t = 0; t < skin.MorphNames.Length; t++)
                if (skin.MorphNames[t] == target)
                {
                    skin.MorphWeights[t] = weight;
                    found = true;
                }
        if (found) Pose(model, model.LastPose ?? model.BindPose);
    }

    // A clip's morph weights at a time, between the frames either side, counted round its length.
    private static float[]? SampleMorphs(ModelAnimation animation, float seconds)
    {
        var frames = animation.FrameMorphWeights;
        if (frames.Length == 0 || animation.MorphChannels.Length == 0) return null;
        var at = seconds * AnimationFps;
        at -= MathF.Floor(at / frames.Length) * frames.Length;
        var first = Math.Min((int)at, frames.Length - 1);
        var (a, b, t) = (frames[first], frames[(first + 1) % frames.Length], at - first);
        var weights = new float[a.Length];
        for (int i = 0; i < weights.Length; i++) weights[i] = a[i] + (b[i] - a[i]) * t;
        return weights;
    }

    // Moves the targets a clip names toward the clip's weights, blend of the way.
    private static void ApplyMorphs(Model model, ModelAnimation animation, float[]? weights, float blend)
    {
        if (weights is null) return;
        for (int c = 0; c < animation.MorphChannels.Length && c < weights.Length; c++)
        {
            var (node, target) = animation.MorphChannels[c];
            foreach (var skin in model.Skins)
                if (skin.Node == node && target < skin.MorphWeights.Length)
                    skin.MorphWeights[target] += (weights[c] - skin.MorphWeights[target]) * blend;
        }
    }

    // A clip's bones at a time, between the frames either side, counted round its length.
    private static Transform[] Sample(ModelAnimation animation, float seconds)
    {
        var frames = animation.FrameCount;
        var at = seconds * AnimationFps;
        at -= MathF.Floor(at / frames) * frames;
        var first = Math.Min((int)at, frames - 1);
        return Mix(animation.FramePoses[first], animation.FramePoses[(first + 1) % frames], at - first);
    }

    // Two poses of the same bones, weight of the way from the first to the second.
    private static Transform[] Mix(Transform[] a, Transform[] b, float weight)
    {
        var mixed = new Transform[a.Length];
        for (int i = 0; i < mixed.Length; i++)
            mixed[i] = new Transform(
                Vector3.Lerp(a[i].Position, b[i].Position, weight),
                Quaternion.Slerp(a[i].Rotation, b[i].Rotation, weight),
                Vector3.Lerp(a[i].Scale, b[i].Scale, weight));
        return mixed;
    }

    // Whether the renderer poses skinned meshes on the GPU, which it does whenever it runs.
    private static bool GpuSkinning =>
        TryRes<Renderer>(out var renderer) && renderer.Context.IsInitialized && renderer.Context.Graphics is GraphicsDevice { CanSkin: true };

    // Moves each skinned mesh's vertices from their rest by the bones' poses, in the model's space:
    // on the GPU, which is handed the joints' matrices, or with no renderer on the CPU, whose posed
    // vertices are then the mesh's own.
    private static void Pose(Model model, Transform[] poses)
    {
        model.LastPose = poses;
        var gpu = GpuSkinning;
        foreach (var skin in model.Skins)
        {
            var mesh = model.Meshes[skin.Mesh];
            // A mesh with morph targets and no skeleton is held by one joint that never moves.
            Matrix4x4[] joints = skin.BoneOfJoint.Length == 0 ? [Matrix4x4.Identity] : new Matrix4x4[skin.BoneOfJoint.Length];
            for (int j = 0; j < skin.BoneOfJoint.Length; j++)
                joints[j] = skin.FromRest[j] * TransformPropagation.ToMatrix(poses[skin.BoneOfJoint[j]]);
            if (gpu && Meshes.IsSkinned(mesh.Id))
            {
                Meshes.PoseSkin(mesh.Id, joints, skin.MorphWeights.Length > 0 ? (float[])skin.MorphWeights.Clone() : null);
                model.GpuPoses[skin.Mesh] = joints;
                continue;
            }

            Meshes.UpdateVertices(mesh.Id, PoseOnCpu(skin, joints));
        }
    }

    // A skinned mesh's vertices at rest moved toward its morph targets by their weights now.
    internal static ModelVertex[] Morphed(SkinnedMesh skin)
    {
        if (skin.MorphWeights.Length == 0) return skin.Rest;
        var moved = (ModelVertex[])skin.Rest.Clone();
        for (int t = 0; t < skin.MorphWeights.Length; t++)
        {
            var weight = skin.MorphWeights[t];
            if (weight == 0) continue;
            var normals = skin.MorphNormals[t];
            for (int v = 0; v < moved.Length; v++)
                moved[v] = moved[v] with
                {
                    Position = moved[v].Position + skin.MorphPositions[t][v] * weight,
                    Normal = normals is null ? moved[v].Normal : moved[v].Normal + normals[v] * weight,
                };
        }
        return moved;
    }

    // A skinned mesh's vertices moved from their rest by its joints, on the CPU.
    internal static ModelVertex[] PoseOnCpu(SkinnedMesh skin, Matrix4x4[] joints)
    {
        var morphed = Morphed(skin);
        var posed = new ModelVertex[skin.Rest.Length];
        for (int v = 0; v < posed.Length; v++)
        {
            var rest = morphed[v];
            Vector3 position = Vector3.Zero, normal = Vector3.Zero;
            for (int k = 0; k < 4; k++)
            {
                float weight = skin.Weights[v * 4 + k];
                if (weight <= 0) continue;
                ref var m = ref joints[skin.Joints[v * 4 + k]];
                position += Vector3.Transform(rest.Position, m) * weight;
                normal += Vector3.TransformNormal(rest.Normal, m) * weight;
            }
            // A vertex no bone holds stays where it rests.
            if (normal == Vector3.Zero) (position, normal) = (rest.Position, rest.Normal);
            posed[v] = rest with { Position = position, Normal = Vector3.Normalize(normal) };
        }
        return posed;
    }

    /// <summary>
    /// Whether <paramref name="animation"/> moves the bones <paramref name="model"/> has: as many,
    /// each under the same parent, and each by the same name where the clip names its bones.
    /// </summary>
    /// <remarks>
    /// raylib compares the counts alone. A clip of another skeleton with as many bones would move
    /// the model's bones by the wrong ones, so the parents are compared too, and the names where an
    /// IQM file of clips alone does not leave them out.
    /// </remarks>
    public static bool IsModelAnimationValid(Model model, ModelAnimation animation)
    {
        if (model.Bones.Length == 0)
            return animation.Bones.Length == 0 && animation.MorphChannels.Length > 0 && model.Skins.Any(s => s.MorphNames.Length > 0);
        if (model.Bones.Length != animation.Bones.Length) return false;
        for (int b = 0; b < model.Bones.Length; b++)
        {
            var (bone, moved) = (model.Bones[b], animation.Bones[b]);
            if (bone.Parent != moved.Parent || (moved.Name.Length > 0 && moved.Name != bone.Name)) return false;
        }
        return true;
    }

    /// <summary>Lets go of a clip. It holds no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimation(ModelAnimation animation) { }

    /// <summary>Lets go of clips. They hold no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimations(ModelAnimation[] animations) { }

    // A model file as Assimp reads it, or null with the reason logged. MagicaVoxel's files,
    // Inter-Quake Models and Model 3D files are read here, as raylib reads them itself, where
    // Assimp reads an IQM's mesh without its skeleton and the Assimp carried reads no M3D.
    private static Scene? ReadModelScene(string fileName, string caller) =>
        ReadModelFile(fileName, caller, (data, path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".vox" => VoxModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            ".iqm" => IqmModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            ".m3d" => M3dModelReader.Read(data, Path.GetFileNameWithoutExtension(path)),
            _ => new AssimpModelReader().ReadFile(path, new SceneImportSettings()),
        });

    private delegate T ModelFileReader<T>(ReadOnlySpan<byte> data, string path);

    // What a reader makes of a model file found beside the program or in the working directory, or
    // null with the reason logged.
    private static T? ReadModelFile<T>(string fileName, string caller, ModelFileReader<T> read) where T : class
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' was not found beside the program or in the working directory.");
            return null;
        }

        try
        {
            // Assimp reads the file itself, so only the readers of this engine's own are given its bytes.
            var data = Path.GetExtension(path).ToLowerInvariant() is ".vox" or ".iqm" or ".m3d" ? File.ReadAllBytes(path) : [];
            return read(data, path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Assimp.AssimpException)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' could not be read: {ex.Message}");
            return null;
        }
    }
}

/// <summary>The bones of a model file and its clips' poses, which <c>LoadModel</c> and <c>LoadModelAnimations</c> both work out the same way.</summary>
internal static class ModelSkeleton
{
    /// <summary>Every node of the scene, parents before children, in the order <c>LoadModel</c> visits them.</summary>
    public static IEnumerable<SceneNode> Walk(Scene scene)
    {
        var stack = new Stack<SceneNode>();
        for (int i = scene.Roots.Count - 1; i >= 0; i--) stack.Push(scene.Roots[i]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (int i = node.Children.Count - 1; i >= 0; i--) stack.Push(node.Children[i]);
        }
    }

    /// <summary>
    /// Each node by name, with its parent node. Of a repeated name the first node without a mesh
    /// is taken, or the first node when each has one.
    /// </summary>
    /// <remarks>
    /// Bones are known by name alone, and Blender's exports give a part's mesh its bone's name, as
    /// raylib's robot names a mesh and a bone each <c>Head</c>. The bone is the node without a mesh,
    /// wherever the two come in the file.
    /// </remarks>
    public static Dictionary<string, (SceneNode Node, SceneNode? Parent)> NodesByName(Scene scene)
    {
        static bool HasMesh(SceneNode node) => node.Components.OfType<SceneMeshPayload>().Any();

        var nodes = new Dictionary<string, (SceneNode Node, SceneNode? Parent)>(StringComparer.Ordinal);
        void Visit(SceneNode node, SceneNode? parent)
        {
            if (!nodes.TryGetValue(node.Name, out var known) || (HasMesh(known.Node) && !HasMesh(node)))
                nodes[node.Name] = (node, parent);
            foreach (var child in node.Children) Visit(child, node);
        }
        foreach (var root in scene.Roots) Visit(root, null);
        return nodes;
    }

    /// <summary>
    /// The model's bones, which are the joints of every skeleton in the file, in the order their
    /// meshes and joints come, each once, with its parent the nearest node above it that is a bone.
    /// </summary>
    public static BoneInfo[] Bones(Scene scene)
    {
        var names = new List<string>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in Walk(scene))
            foreach (var skeleton in node.Components.OfType<SceneSkeletonPayload>())
                foreach (var joint in skeleton.JointNames)
                    if (index.TryAdd(joint, names.Count)) names.Add(joint);

        var nodes = NodesByName(scene);
        var bones = new BoneInfo[names.Count];
        for (int b = 0; b < names.Count; b++)
        {
            int parent = -1;
            var above = nodes.TryGetValue(names[b], out var found) ? found.Parent : null;
            for (; above is not null && parent < 0; above = nodes.TryGetValue(above.Name, out var next) ? next.Parent : null)
                if (index.TryGetValue(above.Name, out var p)) parent = p;
            bones[b] = new BoneInfo(names[b], parent);
        }
        return bones;
    }

    /// <summary>Each bone's model-space matrix with every node at the transform <paramref name="local"/> gives it.</summary>
    public static Matrix4x4[] Pose(BoneInfo[] bones, Dictionary<string, (SceneNode Node, SceneNode? Parent)> nodes, Func<SceneNode, Transform> local)
    {
        var known = new Dictionary<SceneNode, Matrix4x4>();
        Matrix4x4 World(SceneNode node)
        {
            if (known.TryGetValue(node, out var m)) return m;
            var matrix = TransformPropagation.ToMatrix(local(node));
            if (nodes.TryGetValue(node.Name, out var entry) && entry.Parent is { } parent) matrix *= World(parent);
            return known[node] = matrix;
        }

        var pose = new Matrix4x4[bones.Length];
        for (int b = 0; b < bones.Length; b++)
            pose[b] = nodes.TryGetValue(bones[b].Name, out var entry) ? World(entry.Node) : Matrix4x4.Identity;
        return pose;
    }

    /// <summary>A clip sampled at <see cref="Engine3D.AnimationFps"/> into each bone's model-space pose per frame.</summary>
    public static ModelAnimation Sample(Scene scene, SceneAnimationPayload clip, BoneInfo[] bones, Dictionary<string, (SceneNode Node, SceneNode? Parent)> nodes)
    {
        var channels = clip.Channels
            .GroupBy(c => c.TargetNodePath.TrimStart('/'))
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        // raylib's count, the frame at 0 and one each sixtieth of a second that ends inside the clip,
        // so a clip whose length is not a whole number of frames leaves the last part of one out.
        int frames = Math.Max(1, (int)(clip.DurationSeconds * Engine3D.AnimationFps) + 1);
        var poses = new Transform[frames][];
        for (int f = 0; f < frames; f++)
        {
            float time = MathF.Min((float)f / Engine3D.AnimationFps, clip.DurationSeconds);
            var matrices = Pose(bones, nodes, node => channels.TryGetValue(node.Name, out var own) ? At(node.LocalTransform, own, time) : node.LocalTransform);
            poses[f] = new Transform[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                if (!Matrix4x4.Decompose(matrices[b], out var scale, out var rotation, out var position))
                    (scale, rotation, position) = (Vector3.One, Quaternion.Identity, matrices[b].Translation);
                poses[f][b] = new Transform { Position = position, Rotation = rotation, Scale = scale };
            }
        }

        // The weights the clip gives morph targets, a channel each, sampled at the same frames.
        var morphChannels = clip.Channels.Where(c => c.Property == SceneAnimationProperty.MorphWeight).ToArray();
        var morphWeights = new float[frames][];
        for (int f = 0; f < frames; f++)
        {
            float time = MathF.Min((float)f / Engine3D.AnimationFps, clip.DurationSeconds);
            morphWeights[f] = [.. morphChannels.Select(c => Key(c, time).X)];
        }

        return new ModelAnimation
        {
            Name = clip.Name, Bones = bones, FramePoses = poses,
            MorphChannels = [.. morphChannels.Select(c => (c.TargetNodePath.TrimStart('/'), c.MorphTarget))],
            FrameMorphWeights = morphChannels.Length > 0 ? morphWeights : [],
        };
    }

    // A node's transform at a time, each property its channel has taken from the channel and the
    // rest kept from the node.
    private static Transform At(Transform rest, SceneAnimationChannel[] channels, float time)
    {
        foreach (var channel in channels)
        {
            var value = Key(channel, time);
            switch (channel.Property)
            {
                case SceneAnimationProperty.Translation: rest.Position = new Vector3(value.X, value.Y, value.Z); break;
                case SceneAnimationProperty.Rotation: rest.Rotation = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W)); break;
                case SceneAnimationProperty.Scale: rest.Scale = new Vector3(value.X, value.Y, value.Z); break;
            }
        }
        return rest;
    }

    // The channel's value at a time. That is its first key before it and its last after, and
    // between two keys the step's own key, or a blend, spherical for a rotation.
    private static Vector4 Key(SceneAnimationChannel channel, float time)
    {
        var times = channel.TimesSeconds;
        var values = channel.Values;
        if (times.Length == 0) return Vector4.Zero;
        if (time <= times[0]) return values[0];
        if (time >= times[^1]) return values[^1];

        int k = Array.BinarySearch(times, time);
        if (k >= 0) return values[k];
        k = ~k - 1;
        if (channel.Interpolation == SceneAnimationInterpolation.Step) return values[k];

        float t = (time - times[k]) / (times[k + 1] - times[k]);
        if (channel.Property != SceneAnimationProperty.Rotation) return Vector4.Lerp(values[k], values[k + 1], t);
        var a = new Quaternion(values[k].X, values[k].Y, values[k].Z, values[k].W);
        var b = new Quaternion(values[k + 1].X, values[k + 1].Y, values[k + 1].Z, values[k + 1].W);
        var q = Quaternion.Slerp(a, b, t);
        return new Vector4(q.X, q.Y, q.Z, q.W);
    }
}
