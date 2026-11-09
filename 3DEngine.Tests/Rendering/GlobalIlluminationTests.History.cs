using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The screen's probes' frames before left out of a still view, which keeps its light as it was,
// since each probe's kept light is the mean of the frames its rays gave.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Leaving_The_Screen_Probes_Frames_Before_Out_Of_A_Still_View_Keeps_Its_Light()
    {
        // A room lit by an emissive panel at Medium, which stands the screen's probes, the light they
        // give read with their frames before kept and left out (gi.toggle history off). Left out,
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
}
