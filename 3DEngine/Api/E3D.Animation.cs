using System.Numerics;

namespace Engine;

public static partial class E3D
{
    /// <summary>How many frames a second <see cref="LoadModelAnimations"/> samples a clip at.</summary>
    public const int AnimationFps = ModelAnimation.SampledFps;

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
            case ".iqm": return ModelFiles.Read(fileName, "LoadModelAnimations", (data, _) => IqmModelReader.ReadAnimations(data)) ?? [];
            case ".m3d": return ModelFiles.Read(fileName, "LoadModelAnimations", (data, _) => M3dModelReader.ReadAnimations(data)) ?? [];
        }
        if (ModelFiles.ReadScene(fileName, "LoadModelAnimations") is not { } scene) return [];

        var bones = SceneBones.Bones(scene);
        var nodes = SceneBones.NodesByName(scene);
        var clips = new List<ModelAnimation>();
        foreach (var node in SceneBones.Walk(scene))
            foreach (var clip in node.Components.OfType<SceneAnimationPayload>())
                clips.Add(SceneBones.Sample(scene, clip, bones, nodes));

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
        if (animation.KeyframeCount == 0 || !IsModelAnimationValid(model, animation)) return;
        if (frame != MathF.Floor(frame))
        {
            // Between two frames, which the clip is sampled at AnimationFps apart
            UpdateModelAnimationAt(model, animation, frame / AnimationFps);
            return;
        }
        var whole = (int)frame;
        whole = ((whole % animation.KeyframeCount) + animation.KeyframeCount) % animation.KeyframeCount;
        ApplyMorphs(model, animation, animation.KeyframeMorphWeights.Length > whole ? animation.KeyframeMorphWeights[whole] : null, 1);
        Pose(model, animation.KeyframePoses[whole]);
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
        if (animation.KeyframeCount == 0 || !IsModelAnimationValid(model, animation)) return;
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
        if (from.KeyframeCount == 0 || to.KeyframeCount == 0 || !IsModelAnimationValid(model, from) || !IsModelAnimationValid(model, to)) return;
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
        if (under.KeyframeCount == 0 || over.KeyframeCount == 0 || !IsModelAnimationValid(model, under) || !IsModelAnimationValid(model, over)) return;
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
        if (found) Pose(model, model.LastPose ?? model.Skeleton.BindPose);
    }

    // A clip's morph weights at a time, between the frames either side, counted round its length.
    private static float[]? SampleMorphs(ModelAnimation animation, float seconds)
    {
        var frames = animation.KeyframeMorphWeights;
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
        var frames = animation.KeyframeCount;
        var at = seconds * AnimationFps;
        at -= MathF.Floor(at / frames) * frames;
        var first = Math.Min((int)at, frames - 1);
        return Mix(animation.KeyframePoses[first], animation.KeyframePoses[(first + 1) % frames], at - first);
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

            Meshes.UpdateVertices(mesh.Id, Skinning.PoseOnCpu(skin, joints));
        }
    }

    /// <summary>
    /// Whether <paramref name="animation"/> moves the bones <paramref name="model"/> has, as many,
    /// each under the same parent and each by the same name where the clip names its bones.
    /// </summary>
    /// <remarks>
    /// raylib compares the counts alone. A clip of another skeleton with as many bones would move
    /// the model's bones by the wrong ones, so the parents are compared too, and the names where an
    /// IQM file of clips alone does not leave them out.
    /// </remarks>
    public static bool IsModelAnimationValid(Model model, ModelAnimation animation)
    {
        if (model.Skeleton.Bones.Length == 0)
            return animation.Bones.Length == 0 && animation.MorphChannels.Length > 0 && model.Skins.Any(s => s.MorphNames.Length > 0);
        if (model.Skeleton.Bones.Length != animation.Bones.Length) return false;
        for (int b = 0; b < model.Skeleton.Bones.Length; b++)
        {
            var (bone, moved) = (model.Skeleton.Bones[b], animation.Bones[b]);
            if (bone.Parent != moved.Parent || (moved.Name.Length > 0 && moved.Name != bone.Name)) return false;
        }
        return true;
    }

    /// <summary>Lets go of a clip. It holds no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimation(ModelAnimation animation) { }

    /// <summary>Lets go of clips. They hold no GPU objects, so this is for symmetry with raylib.</summary>
    public static void UnloadModelAnimations(ModelAnimation[] animations) { }
}
