using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The coarser cascades' light where only the first cascade's box moves, each cascade's probes
// marching the field from their own cascade outward (gi.own_field) and from the finest.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Second_Cascades_Light_Holds_Where_The_First_Cascades_Box_Moves_Under_A_Roof_Of_Slats()
    {
        // A roof of slats a tenth of a unit wide, two tenths apart, over the ground under a bright
        // sky and the sun, across where the first cascade's side along -x stands, its gaps open in
        // the first cascade's cells of 0.125 and closed in the second's of 0.25. The field's middle
        // moved from x 0.4 to 0.6 moves the first cascade's box a unit and leaves the second's
        // where it lay, so the second cascade's probes stand where they stood, and their merged
        // light is read settled at each. Marched from the finest cascade, the stretch of a second
        // cascade's ray within the first's box meets the slats as the first's cells hold them, so
        // its light moves with that box. Sixty frames at each place read both changes as 240 do,
        // to a thousandth of a percent on the devices here.
        var config = Config.Default.WithWindow("own field", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.125f);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.6f, -1, 0.2f)), Color.White, 3);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var stages = GlobalIlluminationStages.Of((GraphicsDevice)renderer.Context.Graphics!);
        var camera = new Camera3D(new Vector3(0, 1.5f, 3), new Vector3(-3, 1, 0), Vector3.UnitY, 70);

        float[] Settled(float middle, int frames)
        {
            SetSceneFieldCenter(new Vector3(middle, 1, 0));
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(32, 1, 32), stone);
                for (int slat = 0; slat < 17; slat++)
                    DrawModelEx(block, new Vector3(-6 + 0.3f * slat, 2, 0), Vector3.UnitY, 0, new Vector3(0.1f, 0.1f, 8), stone);
                EndMode3D();
                EndDrawing();
            }
            return stages.ReadProbeVolume(renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!, 1, merged: true);
        }

        // The second cascade's merged light's change between the two places of the field's middle,
        // its texels' differences in luminance summed over their light at the first.
        double Moved(bool own)
        {
            settings.OwnFieldMarched = own;
            var before = Settled(0.4f, 60);
            var after = Settled(0.6f, 60);
            double changed = 0, held = 0;
            for (int i = 0; i + 3 < before.Length; i += 4)
            {
                var (a, b) = (Luminance(before, i), Luminance(after, i));
                (changed, held) = (changed + Math.Abs(b - a), held + a);
            }
            return changed / Math.Max(held, 1e-9);
        }

        static double Luminance(float[] texels, int i) => 0.2126 * texels[i] + 0.7152 * texels[i + 1] + 0.0722 * texels[i + 2];

        var own = Moved(own: true);
        var finest = Moved(own: false);
        output.WriteLine($"the second cascade's light moves by {own:P2} marched from its own cascade, {finest:P2} from the finest");
        own.Should().BeLessThan(0.002, $"the second cascade's probes stand where they stood and march its own cells, {own:P2} moved");
        finest.Should().BeGreaterThan(5 * own, $"marched from the finest the slats' gaps open and close with the first cascade's box, {finest:P2} moved against {own:P2}");
        settings.OwnFieldMarched = false;
        UnloadModel(block);
    }
}
