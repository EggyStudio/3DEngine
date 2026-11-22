using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The ray scene's corners grown and packed again by copies the frame records with its own
// commands, so neither waits for the device, the corners a program reserves ahead, and those of a
// mesh that leaves taken by the next that fits in them.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void The_Ray_Scenes_Corners_Grow_And_Are_Packed_Again_Without_The_Frame_Waiting_For_The_Device()
    {
        // Spheres of 8,192 triangles come one a frame, 24,576 corners each, past the buffer's
        // room as it doubles, and the stage's time in each frame that grew it is read beside the
        // frames either side.
        var rays = OpenRayRoom(out var mirror);
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        var balls = new List<Model>();
        var (stage, rooms) = (new List<double>(), new List<int>());
        for (int frame = 0; frame < 40; frame++)
        {
            balls.Add(LoadModelFromMesh(GenMeshSphere(0.1f + 0.001f * frame, 64, 64)));
            RayFrames(1, mirror, () => DrawBalls(balls));
            stage.Add(GlobalIlluminationCpu());
            rooms.Add(rays.Room);
        }
        var grown = Enumerable.Range(1, rooms.Count - 1).Where(i => rooms[i] > rooms[i - 1]).ToList();
        grown.Should().NotBeEmpty("40 spheres of 24,576 corners pass the buffer's room");
        foreach (var at in grown)
        {
            var near = Enumerable.Range(at - 3, 7).Where(i => i != at && i >= 0 && i < stage.Count).Select(i => stage[i]).Order().ToList();
            output.WriteLine($"the frame growing the corners to {rooms[at]:N0} took {stage[at]:0.00} ms of the CPU, the frames beside it {near[near.Count / 2]:0.00} at the median");
        }
        rooms.Should().BeInAscendingOrder("the room doubles as the corners pass it");
        rooms.Zip(rooms.Skip(1)).Where(p => p.Second > p.First).Should().OnlyContain(p => p.Second == p.First * 2, "the room grows twice over as often as a mesh does not fit");

        // A frame that meets nothing new does not wait for the device, nor one that grows the
        // corners to the room a program reserves, nor those whose meshes leave and whose corners
        // are packed again without theirs.
        var steady = device.SingleSubmits;
        RayFrames(4, mirror, () => DrawBalls(balls));
        (device.SingleSubmits - steady).Should().Be(0, "a frame that meets nothing new submits nothing to wait for");
        var (room, makings) = (rays.Room, rays.Makings);
        ReserveRayCorners(rays.Room * 3);
        var before = device.SingleSubmits;
        RayFrames(1, mirror, () => DrawBalls(balls));
        (device.SingleSubmits - before).Should().Be(0, "the corners grown to the room reserved are copied by the frame's own commands");
        rays.Room.Should().Be(room * 4, "three times the room is rounded up to its next doubling");
        (rays.Makings - makings).Should().Be(2, "the corners and their texture coordinates are made again once");
        // The last sphere stands, so the corners given back end short of the buffer's count.
        rays.UnplacedFrames = 8;
        var (held, packings) = (rays.CornerCount, rays.Makings);
        before = device.SingleSubmits;
        RayFrames(12, mirror, () => DrawBalls([balls[^1]]));
        (device.SingleSubmits - before).Should().Be(0, "the corners are packed again without the meshes that left by the frame's own commands");
        (rays.Makings - packings).Should().Be(2, "the corners and their texture coordinates are packed again once");
        rays.CornerCount.Should().Be(36 + 24576, "the mirror's cube and the sphere standing are what the buffer holds packed");
        rays.CornerCount.Should().BeLessThan(held / 8, "the spheres placed no more gave their corners back");
        rays.Room.Should().BeGreaterThanOrEqualTo(GetReservedRayCorners(), "the room packed again keeps the corners reserved");

        // Spheres that left come again, built from their corners written after those copied.
        RayFrames(8, mirror, () => DrawBalls([.. balls.Take(4), balls[^1]]));
        rays.Meshes.Count.Should().Be(6, "the mirror's mesh and the five spheres drawn are placed");
        rays.CornerCount.Should().Be(36 + 5 * 24576, "the four spheres' corners follow those packed");
        foreach (var ball in balls) UnloadModel(ball);
        UnloadModel(mirror);
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Mesh_That_Leaves_The_Ray_Scene_Gives_Its_Corners_To_The_Next_That_Fits_In_Them()
    {
        // Sixteen spheres of 24,576 corners each stand, and one leaves, too few corners for the
        // buffer to be packed again, so the sphere of the same size that comes after takes the
        // corners it left, and the buffer holds no more.
        var rays = OpenRayRoom(out var mirror);
        rays.UnplacedFrames = 8;
        var balls = Enumerable.Range(0, 16).Select(i => LoadModelFromMesh(GenMeshSphere(0.1f + 0.001f * i, 64, 64))).ToList();
        RayFrames(4, mirror, () => DrawBalls(balls));
        var held = rays.CornerCount;
        var standing = rays.Meshes.ToDictionary(m => m.Key, m => m.Value.FirstCorner, ReferenceEqualityComparer.Instance);
        var leaving = balls[5];
        RayFrames(12, mirror, () => DrawBalls(balls.Where(b => b != leaving).ToList()));
        rays.DeadCorners.Should().Be(24576, "the sphere placed no more gives its corners back");
        var left = standing.Keys.Except(rays.Meshes.Keys, ReferenceEqualityComparer.Instance).Should().ContainSingle().Subject;
        var kept = rays.Meshes.Keys.ToHashSet(ReferenceEqualityComparer.Instance);
        var coming = LoadModelFromMesh(GenMeshSphere(0.3f, 64, 64));
        RayFrames(6, mirror, () => DrawBalls([.. balls.Where(b => b != leaving), coming]));
        var came = rays.Meshes.Where(m => !kept.Contains(m.Key)).Should().ContainSingle().Subject;
        came.Value.FirstCorner.Should().Be(standing[left], "the sphere that comes takes the corners the one that left gave back");
        (rays.CornerCount, rays.DeadCorners).Should().Be((held, 0), "the buffer holds no more corners than before");
        foreach (var ball in balls) UnloadModel(ball);
        UnloadModel(coming);
        UnloadModel(mirror);
    }

    // The spheres drawn in rows before the mirror.
    private static void DrawBalls(IReadOnlyList<Model> balls)
    {
        for (int i = 0; i < balls.Count; i++)
            DrawModel(balls[i], new Vector3(-2 + i % 10 * 0.42f, 0.3f + i / 10 * 0.42f, -0.6f), 1, Color.White);
    }

    // The CPU's time in the frame's global illumination, in ms.
    private static double GlobalIlluminationCpu() =>
        GetApp().World.Resource<Engine.Renderer>().Timings.NodeCpu.Where(n => n.Node == "global_illumination").Sum(n => n.Milliseconds);
}
