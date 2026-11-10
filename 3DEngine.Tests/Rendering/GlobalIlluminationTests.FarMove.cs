using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The far cascade's band toward the light past the probes, as the cascade follows a walk.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Surface_In_The_Far_Cascades_Band_Keeps_Its_Light_As_The_Cascade_Moves()
    {
        // A block six units wide ten over open ground 46 units ahead along a walk, inside the far
        // cascade's band and past the counted cascades, seen from below. The eye walks toward it a
        // twentieth of a unit a frame, and the far cascade's corner moves a snap of eight cells at
        // once when the eye passes its grid, as wide as the band, so weighed in that grid the block's
        // underside took the light past the probes in the frame before and the probes' in the frame
        // of the move, half again as bright.
        var config = Config.Default.WithWindow("far move", 160, 96) with { Headless = true, Offscreen = true, Samples = 1, HeadlessFps = 0 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.15f, 2, farCascade: 100);
        SetGlobalIllumination(GlobalIllumination.Medium);
        SetAmbientLight(new Color(120, 160, 230), 1);
        var ground = LoadModelFromMesh(GenMeshCube(600, 1, 600));
        var block = LoadModelFromMesh(GenMeshCube(6, 1, 6));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var plan = () => renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        var at = new Vector3(-46, 10, 0);
        // The mean light of the block's underside in the middle of the window, the eye at x.
        double Frame(float x)
        {
            var eye = new Vector3(x, 2, 0);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(eye, at, Vector3.UnitY, 20));
            DrawModel(ground, new Vector3(0, -0.5f, 0), 1, new Color(110, 140, 80));
            DrawModel(block, at, 1, Color.White);
            EndMode3D();
            EndDrawing();
            var light = device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int py = 40; py < 56; py++)
                for (int px = 70; px < 90; px++)
                {
                    var p = (py * 160 + px) * 4;
                    sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                }
            return sum / (16 * 20);
        }
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 120; frame++)
            Frame(0);
        var far = plan().Cascades - 1;
        var from = plan().BuiltOrigin(far);
        var before = Frame(0);
        var (moved, most) = (-1, 0.0);
        for (int frame = 1; frame <= 300 && (moved < 0 || frame <= moved + 2); frame++)
        {
            var now = Frame(frame * 0.05f);
            if (moved < 0 && plan().BuiltOrigin(far) != from) moved = frame;
            if (moved >= 0) most = Math.Max(most, Math.Abs(now / before - 1));
            before = now;
        }
        UnloadModel(ground);
        UnloadModel(block);
        moved.Should().BePositive("the far cascade follows the eye past its grid");
        most.Should().BeLessThan(0.1, $"the block's light holds within a tenth over the frame the far cascade moved, the walk's frame {moved}, and the two after it");
    }
}
