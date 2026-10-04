using System.Numerics;

namespace Engine;

/// <summary>One bone of a model or an animation, by its name and the index of its parent bone, or -1.</summary>
public readonly record struct BoneInfo(string Name, int Parent);

/// <summary>
/// A clip of a model file, sampled into poses of every bone at <see cref="Engine3D.AnimationFps"/>
/// frames a second, played on a model with <see cref="Engine3D.UpdateModelAnimation"/>.
/// </summary>
/// <remarks>
/// The fields are raylib's. <see cref="FramePoses"/> holds each frame's bones in the model's own
/// space, in the order of <see cref="Bones"/>, which a model loaded from the same file shares.
/// </remarks>
public sealed class ModelAnimation
{
    /// <summary>The clip's name in the file.</summary>
    public string Name { get; init; } = "";

    /// <summary>The bones the clip moves, in the order of each frame's poses.</summary>
    public BoneInfo[] Bones { get; init; } = [];

    /// <summary>For each frame, each bone's pose in the model's space.</summary>
    public Transform[][] FramePoses { get; init; } = [];

    /// <summary>How many bones each frame poses.</summary>
    public int BoneCount => Bones.Length;

    /// <summary>How many frames the clip has.</summary>
    public int FrameCount => FramePoses.Length;
}

/// <summary>What a mesh needs to be posed, which is its vertices at rest and the bones that move each.</summary>
/// <param name="Mesh">The index of the mesh in <see cref="Model.Meshes"/>.</param>
/// <param name="Rest">The mesh's vertices in the model's space, as loaded.</param>
/// <param name="Joints">Four joint indices per vertex, into <paramref name="BoneOfJoint"/>.</param>
/// <param name="Weights">Four weights per vertex, summing to one.</param>
/// <param name="BoneOfJoint">The model bone each of the mesh's joints is.</param>
/// <param name="FromRest">For each joint, model space at rest to the joint's own space.</param>
internal sealed record SkinnedMesh(int Mesh, ModelVertex[] Rest, ushort[] Joints, float[] Weights, int[] BoneOfJoint, Matrix4x4[] FromRest);

public static partial class Engine3D
{
    /// <summary>How many frames a second <see cref="LoadModelAnimations"/> samples a clip at.</summary>
    public const int AnimationFps = 60;

    /// <summary>
    /// Loads every clip of a model file (glTF, FBX, COLLADA and the rest Assimp reads), each
    /// sampled into poses at <see cref="AnimationFps"/> frames a second.
    /// </summary>
    /// <returns>The clips, or none when the file cannot be read or has none, with the reason in the log.</returns>
    public static ModelAnimation[] LoadModelAnimations(string fileName)
    {
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
    /// counted round the clip's length, by moving each skinned mesh's vertices from their rest.
    /// </summary>
    /// <remarks>
    /// The GPU poses the vertices, handed only the joints' matrices, in the frame drawn next, and
    /// the mesh's own vertices stay at rest. With no renderer the CPU poses them and they replace
    /// the mesh's, as raylib does it, at a cost of the mesh's vertex count every call. A model with
    /// no bones, or a clip whose bones are not the model's (<see cref="IsModelAnimationValid"/>),
    /// is left as it is.
    /// </remarks>
    public static void UpdateModelAnimation(Model model, ModelAnimation animation, int frame)
    {
        if (animation.FrameCount == 0 || !IsModelAnimationValid(model, animation)) return;
        frame = ((frame % animation.FrameCount) + animation.FrameCount) % animation.FrameCount;
        Pose(model, animation.FramePoses[frame]);
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
        Pose(model, Mix(Sample(from, fromSeconds), Sample(to, toSeconds), Math.Clamp(weight, 0f, 1f)));
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
        var gpu = GpuSkinning;
        foreach (var skin in model.Skins)
        {
            var mesh = model.Meshes[skin.Mesh];
            var joints = new Matrix4x4[skin.BoneOfJoint.Length];
            for (int j = 0; j < joints.Length; j++)
                joints[j] = skin.FromRest[j] * TransformPropagation.ToMatrix(poses[skin.BoneOfJoint[j]]);
            if (gpu && Meshes.IsSkinned(mesh.Id))
            {
                Meshes.PoseSkin(mesh.Id, joints);
                model.GpuPoses[skin.Mesh] = joints;
                continue;
            }

            Meshes.UpdateVertices(mesh.Id, PoseOnCpu(skin, joints));
        }
    }

    // A skinned mesh's vertices moved from their rest by its joints, on the CPU.
    internal static ModelVertex[] PoseOnCpu(SkinnedMesh skin, Matrix4x4[] joints)
    {
        var posed = new ModelVertex[skin.Rest.Length];
        for (int v = 0; v < posed.Length; v++)
        {
            var rest = skin.Rest[v];
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

    /// <summary>Whether <paramref name="animation"/> moves the bones <paramref name="model"/> has, by name and in order.</summary>
    public static bool IsModelAnimationValid(Model model, ModelAnimation animation) =>
        model.Bones.Length > 0 && model.Bones.AsSpan().SequenceEqual(animation.Bones);

    /// <summary>Lets go of a clip. It holds no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimation(ModelAnimation animation) { }

    /// <summary>Lets go of clips. They hold no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimations(ModelAnimation[] animations) { }

    // A model file as Assimp reads it, or null with the reason logged.
    // Whether a model file found beside the program has clips to play, read once a path, which a
    // ModelRef of it plays through an AnimatedModel. A file not found says no, without a warning,
    // since the asset server may still find it.
    internal static bool HasAnimations(string fileName)
    {
        lock (AnimatedFiles)
        {
            if (AnimatedFiles.TryGetValue(fileName, out var known)) return known;
            var animated = ResolveFile(fileName) is not null && ReadModelScene(fileName, "ModelRef") is { } scene
                && ModelSkeleton.Walk(scene).Any(node => node.Components.OfType<SceneAnimationPayload>().Any());
            return AnimatedFiles[fileName] = animated;
        }
    }

    private static readonly Dictionary<string, bool> AnimatedFiles = [];

    private static Scene? ReadModelScene(string fileName, string caller)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"{caller}: '{fileName}' was not found beside the program or in the working directory.");
            return null;
        }

        try
        {
            return new AssimpModelReader().ReadFile(path, new SceneImportSettings());
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

    /// <summary>Each node by name, the first of a repeated name, with its parent node.</summary>
    public static Dictionary<string, (SceneNode Node, SceneNode? Parent)> NodesByName(Scene scene)
    {
        var nodes = new Dictionary<string, (SceneNode, SceneNode?)>(StringComparer.Ordinal);
        void Visit(SceneNode node, SceneNode? parent)
        {
            nodes.TryAdd(node.Name, (node, parent));
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

        int frames = Math.Max(1, (int)MathF.Ceiling(clip.DurationSeconds * Engine3D.AnimationFps) + 1);
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

        return new ModelAnimation { Name = clip.Name, Bones = bones, FramePoses = poses };
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
