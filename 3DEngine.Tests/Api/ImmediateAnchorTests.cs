using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// The 3D shapes the immediate pass draws, recorded against the place a 3D camera's eye rounds to,
/// read back as what they record into the draw list, in an app whose stores queue without a GPU.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class ImmediateAnchorTests : IDisposable
{
    private readonly App _app = new();

    public ImmediateAnchorTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        UseApp(_app);
        ResetRlgl();
    }

    public void Dispose()
    {
        ForgetRlgl();
        UseApp(null);
    }

    private DrawList List => _app.World.Resource<DrawList>();

    // A room's shapes at a place in the world through a camera there, each middle on a float's step
    // at 1,140,736, an eighth, and their sizes off it, and the positions and anchors they record.
    private (Vector3[] Positions, RenderOrigin[] Anchors) Shapes(Vector3 at)
    {
        List.Clear();
        BeginMode3D(new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(0, 0.5f, 0), Vector3.UnitY, 60));
        DrawCube(at + new Vector3(1.25f, 0.375f, 0.5f), 0.6f, 0.3f, 0.7f, Color.White);
        DrawCubeWires(at + new Vector3(-1.25f, 0.5f, 0.25f), 1.004f, 1.004f, 1.004f, Color.Black);
        DrawSphere(at + new Vector3(0, 1.5f, -0.5f), 0.3f, Color.Red);
        DrawCylinder(at + new Vector3(2, 0, 1), 0.15f, 0.3f, 0.9f, 8, Color.Blue);
        DrawPlane(at + new Vector3(0, 0.125f, 1.5f), new Vector2(0.3f, 0.7f), Color.Green);
        DrawBoundingBox(new BoundingBox(at + new Vector3(-0.5f, 0, -0.5f), at + new Vector3(0.5f, 1, 0.5f)), Color.Gold);
        DrawLine3D(at + new Vector3(-3, 0.125f, 2), at + new Vector3(3, 0.125f, 2), Color.Black);
        rlPushMatrix();
        rlTranslatef(at.X + 0.5f, at.Y + 0.25f, at.Z - 1);
        DrawCube(new Vector3(0, 0.3f, 0), 0.3f, 0.3f, 0.3f, Color.Purple);
        rlPopMatrix();
        EndMode3D();
        var anchors = List.Batches.Where(b => b.DepthTest).Select(b => b.Anchor).ToArray();
        return (List.Vertices.ToArray().Select(v => v.Position).ToArray(), anchors);
    }

    [Theory]
    [InlineData(4096f, 1024f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void Shapes_Far_From_The_Worlds_Origin_Are_Recorded_Against_The_Cameras_Eye_As_At_It(float x, float z)
    {
        var at = new Vector3(x, 0, z);
        var (home, homeAnchors) = Shapes(Vector3.Zero);
        var (far, farAnchors) = Shapes(at);
        // A corner far out is recorded as its middle less the anchor, worked in doubles, plus its
        // offset, so it is the corner at the origin bit for bit, where the middle plus the offset in
        // world floats is rounded to the step there and a box an eighth off its size.
        far.Should().Equal(home, $"the shapes {x} units out are recorded as the shapes at the world's origin");
        farAnchors.Should().HaveSameCount(homeAnchors);
        farAnchors.Zip(homeAnchors, (f, h) => (f.X - h.X, f.Y - h.Y, f.Z - h.Z)).Should().AllBeEquivalentTo(((double)x, 0d, (double)z),
            "each batch is recorded against the camera's eye rounded as the frame's origin is");
    }
}
