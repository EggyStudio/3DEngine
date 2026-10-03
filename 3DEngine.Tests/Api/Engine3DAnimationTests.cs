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
    public void A_Skinned_Model_Loads_Its_Bones_At_Rest()
    {
        var model = LoadModel(Arm);

        model.Bones.Should().Equal(new BoneInfo("Shoulder", -1), new BoneInfo("Elbow", 0));
        model.BindPose[1].Position.Should().Be(new Vector3(0, 1, 0), "the elbow rests a unit above the shoulder");
        Positions(model).Max(p => p.Y).Should().BeApproximately(2, 1e-5f, "the arm stands two units tall");
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
    public void A_Clip_Of_Other_Bones_Leaves_The_Model_As_It_Is()
    {
        var model = LoadModel(Arm);
        var other = new ModelAnimation { Bones = [new BoneInfo("Tail", -1)], FramePoses = [[Transform.Identity]] };

        IsModelAnimationValid(model, other).Should().BeFalse();
        var rest = Positions(model);
        UpdateModelAnimation(model, other, 0);
        Positions(model).Should().Equal(rest);
    }
}
