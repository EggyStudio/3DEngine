using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The ray scene's memory as the window's meshes come and go, the buffers it makes as it meets
// them, what the switches and gi.state say the probes' rays are traced through, and the carried
// probes as the device's rays turn off and on.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void The_Ray_Scenes_Memory_Holds_Level_While_Its_Meshes_Come_And_Go()
    {
        // A mesh no copy has placed for the scene's count of frames gives its structure back, as a
        // world whose land is met and left behind has its chunks' meshes made again and the old
        // ones placed no more. A sphere of 8,192 triangles comes each round and goes the next,
        // drawn three times for twelve frames, the scene's count of frames set to eight.
        var rays = OpenRayRoom(out var mirror);
        rays.UnplacedFrames = 8;
        var bytes = new List<long>();
        for (int round = 0; round < 24; round++)
        {
            var ball = LoadModelFromMesh(GenMeshSphere(0.4f + 0.005f * round, 64, 64));
            RayFrames(12, mirror, () =>
            {
                for (int i = 0; i < 3; i++)
                    DrawModel(ball, new Vector3(-1.2f + 1.2f * i, 1.2f, -0.6f), 1, Color.White);
            });
            UnloadModel(ball);
            bytes.Add(rays.Bytes);
        }
        output.WriteLine($"the scene places {rays.Meshes.Count} meshes after 24 rounds and holds {string.Join(", ", bytes.Select(b => $"{b / 1048576.0:0.0}"))} MB round by round");
        rays.Meshes.Count.Should().BeLessThanOrEqualTo(3, "the mirror's mesh and the last round's sphere are placed and the rest given back");
        // Its corners are made again without those of the meshes given back once they are half the
        // buffer, so what it holds rises and falls round by round, and rises no higher.
        bytes.Skip(12).Max().Should().BeLessThanOrEqualTo(bytes.Take(12).Max(),
            $"the scene holds no more in the last twelve rounds than the first, {string.Join(", ", bytes.Select(b => $"{b / 1048576.0:0.0}"))} MB");
        UnloadModel(mirror);
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void The_Ray_Scene_Makes_Its_Buffers_In_Steps_As_It_Meets_A_Mesh_A_Frame()
    {
        // Land met as the eye flies brings a mesh or more a frame, each built into the scene. The
        // builds share a scratch, and the scratch, the corners and the ring of copies are made again
        // at twice what they need where they outgrow it, so 96 meshes met a frame apart make few
        // buffers where a scratch of each build's own made one a mesh.
        var rays = OpenRayRoom(out var mirror);
        var made = new List<Model>();
        var before = rays.Makings;
        for (int frame = 0; frame < 96; frame++)
        {
            made.Add(LoadModelFromMesh(GenMeshCube(0.2f, 0.2f + 0.002f * frame, 0.2f)));
            RayFrames(1, mirror, () =>
            {
                for (int i = 0; i < made.Count; i++)
                    DrawModel(made[i], new Vector3(-2 + i % 12 * 0.35f, 0.3f + i / 12 * 0.35f, -0.6f), 1, Color.White);
            });
        }
        output.WriteLine($"96 meshes met a frame apart make {rays.Makings - before} buffers");
        rays.Meshes.Count.Should().Be(97, "every cube and the mirror are placed");
        (rays.Makings - before).Should().BeLessThanOrEqualTo(24, "the scene's buffers grow in steps as it meets a mesh a frame");
        foreach (var model in made) UnloadModel(model);
        UnloadModel(mirror);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Meshes_Switch_And_The_State_Say_What_The_Probes_Rays_Are_Traced_Through()
    {
        // The switch of the meshes answers what the probes' rays are traced through, the field where
        // the device traces no rays or High left them off for its memory, and gi.state names why
        // the field traces what the device's rays would.
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.15f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        RayFrames(4, block, () => { });
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
        {
            var said = GlobalIlluminationCommands.ToggleBounce("meshes", "on");
            RayFrames(2, block, () => { });
            var state = GlobalIlluminationCommands.IlluminationState(GetApp().World);
            if (device.CanQueryRays)
            {
                said.Should().EndWith("the probes' rays traced through the meshes");
                state.Should().Contain("what the field misses through the device's rays");
                // As High leaves them off where their structures do not fit the device's memory.
                (settings.RaysOff, settings.RaysLeftOff) = (true, "the device's memory is full");
                RayFrames(2, block, () => { });
                GlobalIlluminationCommands.ToggleBounce("meshes", "on").Should()
                    .EndWith("the probes' rays traced through the field, the device's rays left off since its memory did not hold their structures");
                GlobalIlluminationCommands.IlluminationState(GetApp().World).Should()
                    .Contain("the device's rays left off since its memory did not hold their structures, the field tracing what they would, the device's memory is full");
                GlobalIlluminationCommands.Rays("on");
                settings.RaysLeftOff.Should().BeNull("gi.rays on clears why High left them off");
                GlobalIlluminationCommands.Rays("off");
                GlobalIlluminationCommands.ToggleBounce("meshes", "on").Should().EndWith("the probes' rays traced through the field, the device's rays off by gi.rays");
                GlobalIlluminationCommands.Rays("on");
            }
            else
            {
                said.Should().EndWith("the probes' rays traced through the field, the device tracing no rays");
                state.Should().Contain("the device traces no rays, the field tracing what they would");
            }
        }
        UnloadModel(block);
    }

    // A room of one cascade at High with a mirror, as the top level's test opens it, and the
    // scene the device's rays meet.
    private static GpuRayScene OpenRayRoom(out Model mirror)
    {
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.15f, 1);
        SetAmbientLight(Color.White, 0.3f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.2f, -1, 0.3f)), Color.White, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        RayFrames(4, mirror, () => { });
        var rays = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Rays;
        rays.Should().NotBeNull("High traces the device's rays here");
        return rays!;
    }

    // Frames of the room, the model given standing as the mirror and the rest drawn after it.
    private static void RayFrames(int count, Model mirror, Action draw)
    {
        var camera = new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50);
        for (int frame = 0; frame < count; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            draw();
            EndMode3D();
            EndDrawing();
        }
    }
}
