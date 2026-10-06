using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Skeletal animation through the flat API, on <c>arm.gltf</c>: an arm standing from y 0 to y 2
/// with its elbow at y 1, whose clip "bend" turns the elbow 90 degrees about Z over a second.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DAnimationTests : IDisposable
{
    private static readonly string Arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
    private readonly App _app = new();

    public Engine3DAnimationTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<MeshStore>();
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    private Vector3[] Positions(Model model)
    {
        _app.World.Resource<MeshStore>().TryGetData(model.Meshes[0].Id, out var vertices, out _).Should().BeTrue();
        return vertices.Select(v => v.Position).ToArray();
    }

    [Fact]
    public void A_Clip_Plays_On_A_Bone_And_Those_Below_It_Over_Another()
    {
        // Summit's hero, six bones under its hips, whose jump raises both arms and whose run swings them.
        var hero = Path.Combine(AppContext.BaseDirectory, "resources", "hero.gltf");
        var model = LoadModel(hero);
        var clips = LoadModelAnimations(hero);
        var (run, jump) = (clips.Single(c => c.Name == "run"), clips.Single(c => c.Name == "jump"));
        var rest = Positions(model);
        var leftArm = Enumerable.Range(0, rest.Length).Where(i => rest[i].X < -0.255f).ToArray();
        var rightArm = Enumerable.Range(0, rest.Length).Where(i => rest[i].X > 0.255f).ToArray();
        static void Near(Vector3[] actual, Vector3[] expected, string because)
        {
            actual.Length.Should().Be(expected.Length);
            for (int i = 0; i < actual.Length; i++)
                Vector3.Distance(actual[i], expected[i]).Should().BeLessThan(1e-4f, because);
        }

        UpdateModelAnimationAt(model, jump, 0.2f);
        var jumping = Positions(model);
        UpdateModelAnimationAt(model, run, 0.2f);
        var running = Positions(model);

        UpdateModelAnimationLayer(model, run, 0.2f, jump, 0.2f, "Hips");
        Near(Positions(model), jumping, "a layer from the root bone poses the whole body");

        UpdateModelAnimationLayer(model, run, 0.2f, jump, 0.2f, "ArmL");
        var layered = Positions(model);
        leftArm.Max(i => layered[i].Y).Should().BeGreaterThan(1.8f, "the left arm is raised as the jump raises it");
        leftArm.Max(i => running[i].Y).Should().BeLessThan(1.5f, "where the run leaves it down");
        Near([.. rightArm.Select(i => layered[i])], [.. rightArm.Select(i => running[i])], "the right arm swings with the run");

        UpdateModelAnimationLayer(model, run, 0.2f, jump, 0.2f, "ArmL", weight: 0);
        Near(Positions(model), running, "a weight of 0 leaves the first clip alone");
    }

    [Fact]
    public void A_Morph_Target_Moves_The_Mesh_By_Its_Weight_Set_Or_Played()
    {
        // A strip one unit tall whose target "Raise" lifts its top edge a unit, and whose clip
        // "pulse" takes that weight from 0 to 1 and back over a second. build/make-morph-gltf.py.
        var file = Path.Combine(AppContext.BaseDirectory, "resources", "morph.gltf");
        var model = LoadModel(file);
        float Top() => Positions(model).Max(p => p.Y);
        Top().Should().BeApproximately(1, 1e-5f, "the strip rests a unit tall");

        SetModelMorphWeight(model, "Raise", 1);
        Top().Should().BeApproximately(2, 1e-5f, "at full weight the top edge is a unit higher");
        SetModelMorphWeight(model, "Raise", 0.5f);
        Top().Should().BeApproximately(1.5f, 1e-5f);

        var pulse = LoadModelAnimations(file).Should().ContainSingle().Subject;
        pulse.Name.Should().Be("pulse");
        IsModelAnimationValid(model, pulse).Should().BeTrue("a clip of weights fits a model of targets with no skeleton");
        UpdateModelAnimationAt(model, pulse, 0.5f);
        Top().Should().BeApproximately(2, 1e-3f, "halfway through the clip the weight is 1");
        UpdateModelAnimationAt(model, pulse, 0.25f);
        Top().Should().BeApproximately(1.5f, 1e-3f, "a quarter through it is a half");
    }

    [Fact]
    public void A_Clip_On_Part_Of_The_Skeleton_Moves_The_Morph_Targets_It_Plays()
    {
        // The strip of morph.gltf held by one bone, "Root", with a clip at a weight of 0 and one
        // at 1, both keeping the bone still. build/make-morph-gltf.py.
        var file = Path.Combine(AppContext.BaseDirectory, "Api", "layered-morph.gltf");
        var model = LoadModel(file);
        var clips = LoadModelAnimations(file);
        var (rest, lift) = (clips.Single(c => c.Name == "rest"), clips.Single(c => c.Name == "lift"));
        float Top() => Positions(model).Max(p => p.Y);

        UpdateModelAnimationAt(model, rest, 0.5f);
        Top().Should().BeApproximately(1, 1e-3f, "the first clip alone keeps the strip at rest");
        UpdateModelAnimationLayer(model, rest, 0.5f, lift, 0.5f, "Root");
        Top().Should().BeApproximately(2, 1e-3f, "the clip over it raises the top edge by its target");
        UpdateModelAnimationLayer(model, rest, 0.5f, lift, 0.5f, "Root", weight: 0.5f);
        Top().Should().BeApproximately(1.5f, 1e-3f, "half of the way at half weight");
        UpdateModelAnimationLayer(model, rest, 0.5f, lift, 0.5f, "Nowhere");
        Top().Should().BeApproximately(1, 1e-3f, "a bone the model does not have leaves the first clip alone");
    }

    [Fact]
    public void A_Skinned_Model_Loads_Its_Bones_At_Rest()
    {
        var model = LoadModel(Arm);

        model.Bones.Should().Equal(new BoneInfo("Shoulder", -1), new BoneInfo("Elbow", 0));
        model.BindPose[1].Position.Should().Be(new Vector3(0, 1, 0), "the elbow rests a unit above the shoulder");
        Positions(model).Max(p => p.Y).Should().BeApproximately(2, 1e-5f, "the arm stands two units tall");
    }

    [Fact]
    public void A_Clip_Has_Raylibs_Count_Of_Frames_Which_Leaves_Out_A_Part_Frame_At_Its_End()
    {
        // The arm's clip shortened to 0.99 seconds, 59.4 sixtieths, so raylib's count is the frame
        // at 0 and 59 more, where rounding up would add a 61st.
        var gltf = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Arm))!;
        var input = gltf["animations"]![0]!["samplers"]![0]!["input"]!.GetValue<int>();
        var view = gltf["bufferViews"]![gltf["accessors"]![input]!["bufferView"]!.GetValue<int>()]!;
        var uri = gltf["buffers"]![0]!["uri"]!.GetValue<string>();
        var bytes = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
        float[] times = [0, 0.495f, 0.99f];
        for (int i = 0; i < times.Length; i++) BitConverter.TryWriteBytes(bytes.AsSpan(view["byteOffset"]!.GetValue<int>() + i * 4), times[i]);
        gltf["buffers"]![0]!["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(bytes);
        gltf["accessors"]![input]!["max"] = new System.Text.Json.Nodes.JsonArray(0.99f);

        using var folder = new TestFolder("engine-clip-test-");
        File.WriteAllText(folder.File("arm.gltf"), gltf.ToJsonString());
        LoadModelAnimations(folder.File("arm.gltf")).Should().ContainSingle().Which.FrameCount.Should().Be(60);
    }

    [Fact]
    public void A_Clip_Is_Sampled_At_The_Animation_Rate_With_Model_Space_Poses()
    {
        var clip = LoadModelAnimations(Arm).Should().ContainSingle().Subject;

        clip.Name.Should().Be("bend");
        clip.FrameCount.Should().Be(AnimationFps + 1, "a one second clip has a frame at each end");
        var bent = clip.FramePoses[^1][1];
        bent.Position.Should().Be(new Vector3(0, 1, 0), "the elbow turns in place");
        Vector3.Transform(Vector3.UnitY, bent.Rotation).X.Should().BeApproximately(-1, 1e-5f, "a quarter turn about Z points up to -X");
        IsModelAnimationValid(LoadModel(Arm), clip).Should().BeTrue();
    }

    [Fact]
    public void Posing_Moves_The_Vertices_The_Bones_Hold_And_Frame_Zero_Is_The_Rest()
    {
        var model = LoadModel(Arm);
        var clip = LoadModelAnimations(Arm)[0];
        var rest = Positions(model);

        UpdateModelAnimation(model, clip, 0);
        Positions(model).Should().BeEquivalentTo(rest, o => o.Using<float>(c => c.Subject.Should().BeApproximately(c.Expectation, 1e-4f)).WhenTypeIs<float>(),
            "the first frame is the pose the arm was modeled in");

        UpdateModelAnimation(model, clip, clip.FrameCount - 1);
        var bent = Positions(model);
        int tip = Array.FindIndex(rest, p => p.Y > 1.99f);
        // A quarter turn about the elbow takes a point (x, 1) above it to (-1, x) beside it.
        bent[tip].X.Should().BeApproximately(-1, 0.01f, "the tip swings a unit toward -X");
        bent[tip].Y.Should().BeApproximately(1 + rest[tip].X, 0.01f, "and comes down to the elbow's height");
        int foot = Array.FindIndex(rest, p => p.Y < 0.01f);
        bent[foot].Should().Be(rest[foot], "the shoulder's box does not move");
    }

    [Fact]
    public void A_Bone_Named_As_A_Mesh_Node_Before_It_Is_Found_As_The_Bone()
    {
        // As raylib's robot has it: a mesh and a bone each named Head, the mesh first.
        var mesh = new SceneNode { Name = "Head" };
        mesh.Components.Add(new SceneMeshPayload { Positions = [], Indices = [] });
        var bone = new SceneNode { Name = "Head" };
        var neck = new SceneNode { Name = "Neck" };
        neck.Children.Add(bone);
        var armature = new SceneNode { Name = "Armature" };
        armature.Children.Add(mesh);
        armature.Children.Add(neck);
        var scene = new Scene();
        scene.Roots.Add(armature);

        var nodes = ModelSkeleton.NodesByName(scene);

        nodes["Head"].Node.Should().BeSameAs(bone, "the bone is the node without a mesh");
        nodes["Head"].Parent.Should().BeSameAs(neck);
    }

    [Fact]
    public void A_Frame_Between_Two_Is_A_Blend_Of_Them_As_In_Raylib()
    {
        var model = LoadModel(Arm);
        var clip = LoadModelAnimations(Arm)[0];

        UpdateModelAnimationAt(model, clip, 30.5f / AnimationFps);
        var sampled = Positions(model);
        UpdateModelAnimation(model, clip, 30.5f);
        Positions(model).Should().BeEquivalentTo(sampled, o => o.Using<float>(c => c.Subject.Should().BeApproximately(c.Expectation, 1e-4f)).WhenTypeIs<float>(),
            "half a frame poses halfway, as the clip sampled at that time does");

        UpdateModelAnimation(model, clip, 30);
        var whole = Positions(model);
        UpdateModelAnimation(model, clip, 31);
        Positions(model).Should().NotBeEquivalentTo(whole, "and the frames either side differ");
    }

    [Fact]
    public void An_Iqm_Clip_That_Names_No_Bones_Poses_A_Model_Of_The_Same_Skeleton()
    {
        using var folder = new TestFolder("engine-iqm-");
        var (modelPath, clipPath) = (Path.Combine(folder.Path, "tri.iqm"), Path.Combine(folder.Path, "wave.iqm"));
        File.WriteAllBytes(modelPath, Engine.Tests.Assets.Models.IqmModelReaderTests.Triangle());
        File.WriteAllBytes(clipPath, Engine.Tests.Assets.Models.IqmModelReaderTests.Triangle(joints: false, mesh: false));

        var model = LoadModel(modelPath);
        var clip = LoadModelAnimations(clipPath).Single();
        model.Bones.Should().Equal(new BoneInfo("root", -1), new BoneInfo("tip", 0));

        IsModelAnimationValid(model, clip).Should().BeTrue("as many bones under the same parents, the clip naming none");
        UpdateModelAnimation(model, clip, 1);
        Positions(model)[2].Y.Should().BeApproximately(2, 1e-4f, "the corner the tip holds rises with it");
        Positions(model)[0].Should().Be(Vector3.Zero, "and the root's stay");

        var other = new ModelAnimation { Bones = [new BoneInfo("", -1), new BoneInfo("", -1)], FramePoses = [[Transform.Identity, Transform.Identity]] };
        IsModelAnimationValid(model, other).Should().BeFalse("a bone under another parent is another skeleton's");
    }

    [Fact]
    public void An_M3d_Action_Poses_Its_Model_With_Raylibs_Bone_That_Never_Moves()
    {
        using var folder = new TestFolder("engine-m3d-");
        var path = Path.Combine(folder.Path, "tri.m3d");
        File.WriteAllBytes(path, Engine.Tests.Assets.Models.M3dModelReaderTests.Triangle());

        var model = LoadModel(path);
        var clip = LoadModelAnimations(path).Single();
        model.Bones.Select(b => b.Name).Should().Equal("root", "tip", "NO BONE");
        model.Meshes.Single().VertexCount.Should().Be(3, "three vertices of its own for each face, as raylib's");

        IsModelAnimationValid(model, clip).Should().BeTrue();
        UpdateModelAnimation(model, clip, 1);
        Positions(model)[2].Y.Should().BeApproximately(1.5f, 1e-4f, "the corner the tip holds rises halfway to the action's last key");
    }

    [Fact]
    public void A_Clip_Of_Other_Bones_Leaves_The_Model_As_It_Is()
    {
        var model = LoadModel(Arm);
        var other = new ModelAnimation { Bones = [new BoneInfo("Tail", -1)], FramePoses = [[Transform.Identity]] };

        IsModelAnimationValid(model, other).Should().BeFalse();
        var rest = Positions(model);
        UpdateModelAnimation(model, other, 0);
        Positions(model).Should().Equal(rest);
    }

    // Where a point of the forearm at rest is when the elbow, at (0, 1), has turned by the given
    // degrees about Z.
    private static Vector2 Turned(Vector3 rest, float degrees)
    {
        var (sin, cos) = MathF.SinCos(float.DegreesToRadians(degrees));
        var x = rest.X;
        var y = rest.Y - 1;
        return new Vector2(x * cos - y * sin, 1 + x * sin + y * cos);
    }

    private Vector2 Top(Model model, int index)
    {
        var p = Positions(model)[index];
        return new Vector2(p.X, p.Y);
    }

    [Fact]
    public void A_Clip_Sampled_Between_Two_Frames_Turns_The_Bone_Between_Them()
    {
        var model = LoadModel(Arm);
        var clip = LoadModelAnimations(Arm)[0];
        var rest = Positions(model);
        var top = Array.IndexOf(rest, rest.MaxBy(p => p.Y));

        // Frames 30 and 31 are 45 and 46.5 degrees, so halfway is 45.75, which no whole frame has.
        UpdateModelAnimationAt(model, clip, 30.5f / AnimationFps);
        Vector2.Distance(Top(model, top), Turned(rest[top], 45.75f)).Should().BeLessThan(1e-3f);
    }

    [Fact]
    public void Two_Clips_Blend_By_Weight()
    {
        var model = LoadModel(Arm);
        var clip = LoadModelAnimations(Arm)[0];
        var rest = Positions(model);
        var top = Array.IndexOf(rest, rest.MaxBy(p => p.Y));

        // The clip's first frame, at rest, and its last, bent a quarter turn, half each.
        UpdateModelAnimationBlend(model, clip, 0, clip, (clip.FrameCount - 1) / (float)AnimationFps, 0.5f);
        Vector2.Distance(Top(model, top), Turned(rest[top], 45)).Should().BeLessThan(1e-3f);

        UpdateModelAnimationBlend(model, clip, 0, clip, (clip.FrameCount - 1) / (float)AnimationFps, 0);
        Vector2.Distance(Top(model, top), Turned(rest[top], 0)).Should().BeLessThan(1e-3f, "weight 0 is the first clip alone");
    }
}
