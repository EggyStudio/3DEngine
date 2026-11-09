using System.Numerics;

namespace Engine;

/// <summary>The bones of a model file and its clips' poses, which <c>LoadModel</c> and <c>LoadModelAnimations</c> both work out the same way.</summary>
internal static class SceneBones
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

    /// <summary>A clip sampled at <see cref="ModelAnimation.SampledFps"/> into each bone's model-space pose per frame.</summary>
    public static ModelAnimation Sample(Scene scene, SceneAnimationPayload clip, BoneInfo[] bones, Dictionary<string, (SceneNode Node, SceneNode? Parent)> nodes)
    {
        var channels = clip.Channels
            .GroupBy(c => c.TargetNodePath.TrimStart('/'))
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        // raylib's count, the frame at 0 and one each sixtieth of a second that ends inside the clip,
        // so a clip whose length is not a whole number of frames leaves the last part of one out.
        int frames = Math.Max(1, (int)(clip.DurationSeconds * ModelAnimation.SampledFps) + 1);
        var poses = new Transform[frames][];
        for (int f = 0; f < frames; f++)
        {
            float time = MathF.Min((float)f / ModelAnimation.SampledFps, clip.DurationSeconds);
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
            float time = MathF.Min((float)f / ModelAnimation.SampledFps, clip.DurationSeconds);
            morphWeights[f] = [.. morphChannels.Select(c => Key(c, time).X)];
        }

        return new ModelAnimation
        {
            Name = clip.Name, Bones = bones, KeyframePoses = poses,
            MorphChannels = [.. morphChannels.Select(c => (c.TargetNodePath.TrimStart('/'), c.MorphTarget))],
            KeyframeMorphWeights = morphChannels.Length > 0 ? morphWeights : [],
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
