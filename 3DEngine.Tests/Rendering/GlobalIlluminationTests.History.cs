using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The screen's probes' frames before, left out of a still view, which keeps its light as it was,
// since each probe's kept light is the mean of the frames its rays gave, and kept through a slow
// turn over a surface whose probes face many ways.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Leaving_The_Screen_Probes_Frames_Before_Out_Of_A_Still_View_Keeps_Its_Light()
    {
        // A room lit by an emissive panel at Medium, which stands the screen's probes, the light
        // they give read with their frames before kept and left out (gi.history off). Left out,
        // the count of frames a probe kept was never set and took whatever the GPU held there, and
        // the probes gave less light than with the frames kept, though the mean of sixteen frames
        // without them is as dark as one. Set to none, they give a frame's rays alone, which
        // differ from the mean of sixteen by those rays' noise.
        Open();
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = Color.White;
        panel.Materials[0].EmissiveIntensity = 8;
        var gray = new Color(160, 160, 160);
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), gray);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), gray);
            DrawModelEx(slab, new Vector3(0, 1.5f, -1.85f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), gray);
            DrawModelEx(slab, new Vector3(-1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), gray);
            DrawModelEx(slab, new Vector3(1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), gray);
            DrawModelEx(panel, new Vector3(0, 2.97f, 0), Vector3.UnitY, 0, new Vector3(1.2f, 0.06f, 1.2f), Color.White);
        }
        SetGlobalIllumination(GlobalIllumination.Medium);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        // The mean of the light the screen's probes on the room's surfaces give the model pass,
        // after frames enough for the field to settle and the probes to keep their sixteen.
        float Screen()
        {
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 30; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
                Draw();
                EndMode3D();
                EndDrawing();
            }
            var screen = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
            var (blended, geometry) = (device.ReadFloats(screen.Blended), device.ReadFloats(screen.Geometry));
            var held = Enumerable.Range(0, screen.Across * screen.Down).Where(probe => geometry[probe * 4 + 3] >= 0 && blended[probe * 4 + 3] >= 0.5f).ToList();
            held.Should().HaveCountGreaterThan(20, "the room's surfaces hold the screen's probes");
            return held.Average(probe => blended[probe * 4 + 1]);
        }
        var kept = Screen();
        settings.HistoryOff = true;
        var left = Screen();
        settings.HistoryOff = false;
        left.Should().BeApproximately(kept, kept * 0.08f, $"a still view's light is the mean of its frames either way, {kept:0.0000} kept and {left:0.0000} left out");
        UnloadModel(slab);
        UnloadModel(panel);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Medium)]
    [InlineData(GlobalIllumination.High)]
    public void In_A_Slow_Turn_The_Screen_Probes_On_A_Fluted_Wall_Keep_The_Frames_Befores_Light(GlobalIllumination quality)
    {
        // A wall of eight-sided columns side by side, lit by a lamp, the camera turning a tenth of
        // a degree a frame. A probe takes the light the four probes of the frame before around its
        // place kept, and where it refused those that faced more than a little away from it, a
        // probe on a column's slanted face found the four on the faces either side refused, kept
        // this frame's rays alone, and showed their noise from frame to frame. Taken by their
        // distance from the eye alone, the slanted faces' probes keep the frames before's light.
        var config = Config.Default.WithWindow("gi test", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.15f, 2);
        SetGlobalIllumination(quality);
        CreatePointLight(new Vector3(0, 2, 1), new Color(255, 236, 210), 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var column = LoadModelFromMesh(GenMeshCylinder(0.12f, 3.5f, 8));
        var gray = new Color(160, 160, 160);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var (eye, yaw) = (new Vector3(0, 1.5f, 4.5f), 0f);
        var (slanted, lost) = (0, 0);
        for (int frame = 0; frame < 140; frame++)
        {
            if (frame >= 100) yaw += 0.1f * MathF.PI / 180;
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(eye, eye + new Vector3(-MathF.Sin(yaw), -0.25f, -MathF.Cos(yaw)), Vector3.UnitY, 60));
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(10, 0.3f, 10), gray);
            DrawModelEx(slab, new Vector3(0, 2, -3.15f), Vector3.UnitY, 0, new Vector3(10, 4, 0.3f), gray);
            for (int at = -12; at <= 12; at++)
                DrawModel(column, new Vector3(at * 0.22f, 0, -2.9f), 1, gray);
            EndMode3D();
            EndDrawing();
            if (frame < 105) continue;
            // The probes on the columns' slanted faces, and of them those whose light the frames
            // before gave less than half of.
            var screen = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
            var (blended, geometry) = (device.ReadFloats(screen.Blended), device.ReadFloats(screen.Geometry));
            var faces = Enumerable.Range(0, screen.Across * screen.Down)
                .Where(probe => geometry[probe * 4 + 3] >= 0 && Math.Abs(geometry[probe * 4 + 1]) < 0.3f && Math.Abs(geometry[probe * 4]) > 0.2f)
                .ToList();
            slanted += faces.Count;
            lost += faces.Count(probe => blended[probe * 4 + 3] < 1.5f);
        }
        UnloadModel(slab);
        UnloadModel(column);
        slanted.Should().BeGreaterThan(35 * 100, "the columns' slanted faces hold probes through the turn");
        ((float)lost / slanted).Should().BeLessThan(0.01f,
            $"each keeps the frames before's light as the view turns, {lost} of {slanted} probes over the turn's frames kept this frame's alone");
    }
}
