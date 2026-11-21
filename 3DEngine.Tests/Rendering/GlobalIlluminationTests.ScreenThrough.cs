using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A floor under a roof that lets a share of the light through, read against a path-traced reference
// with the screen's probes.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData("slab", 2.5f, 0.2164f, 0.063f, 0.371f)]
    [InlineData("canopy", 5.5f, 0.1191f, 0.246f, 0.403f)]
    public void A_Floor_Under_A_Roof_Letting_Half_The_Light_Through_Takes_The_Traced_Light_With_The_Screens_Probes(string roof, float height,
        float traced, float solid, float half)
    {
        // The slab's floor and the canopy's of the tests before, at High, where the screen's probes
        // stand on the floor beside the world's, read in linear light from the frame before the
        // bloom with no roof, a solid one and one letting half the light through a unit. Each reads
        // within a twentieth of the open floor's traced light of the path-traced reference's over
        // the pixels read (BounceReference.Trace, 2048 paths a pixel, the first face reflecting by
        // its gloss as the model pass draws it), the open floor's given and the others as shares of
        // it.
        FloorsTakeTheirTrace(roof, height, traced, solid, half);
    }

    // The roof's scene's floors, open, under the solid roof and under the half one, each within a
    // twentieth of the open floor's traced light of the reference's, given as the open floor's
    // traced light and the others' shares of it. The share of the light the solid roof holds back
    // that the half one lets through is told and bounds nothing, since a floor lit under the solid
    // roof that the reference holds dark takes it nearer.
    private static void FloorsTakeTheirTrace(string roof, float height, float traced, float solid, float half)
    {
        var scene = RoofSceneOf(roof, height);
        var read = scene.Floors();
        foreach (var (name, light, reference) in new[] { ("open", read.Open, 1f), ("solid", read.Solid, solid), ("half", read.Half, half) })
            (light / traced).Should().BeApproximately(reference, 0.05f,
                $"the {roof}'s floor {name} takes the light the trace gives, {read.Open:0.0000} open, {read.Solid:0.0000} solid and {read.Half:0.0000} half " +
                $"against the open floor's traced {traced:0.0000}, the share the half roof lets through {(read.Half - read.Solid) / (read.Open - read.Solid):0.000}");
        scene.Unload();
    }

    // A floor of the light-through tests under a roof, its eye and the pixels read: the slab's, a
    // dark green slab twenty across; the canopy's, leaf blocks twenty by twenty and two deep; and
    // the leaves', one slab thirty across over a green floor past the screen's probes' reach.
    private sealed record RoofScene(string Roof, float Height, Camera3D Camera, Vector3 Point, int Width, int Depth, Model Ground)
    {
        private readonly Model _solid = Leaves(Roof, 0);
        private readonly Model _half = Leaves(Roof, 0.5f);

        private static Model Leaves(string roof, float through)
        {
            var model = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            if (roof != "slab") model.Materials[0].Roughness = 1;
            model.Materials[0].LightThrough = through;
            return model;
        }

        // The scene with no roof, or under the roof given.
        public void Draw(Model? roof)
        {
            switch (Roof)
            {
                case "slab":
                    DrawModelEx(Ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(128, 128, 128));
                    if (roof is { } slab) DrawModelEx(slab, new Vector3(0, Height, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
                    break;
                case "canopy":
                    DrawModelEx(Ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(80, 1, 80), new Color(128, 128, 128));
                    if (roof is { } block)
                        for (int x = -10; x < 10; x++)
                            for (int z = -16; z < 4; z++)
                                for (int y = 0; y < 2; y++)
                                    DrawModel(block, new Vector3(x + 0.5f, Height + y, z + 0.5f), 1, new Color(60, 110, 45));
                    break;
                default:
                    DrawModelEx(Ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(80, 1, 80), new Color(90, 140, 60));
                    if (roof is { } leaves) DrawModelEx(leaves, new Vector3(0, Height, -4), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(50, 110, 40));
                    break;
            }
        }

        // No roof, the solid one or the one letting half the light through.
        public Model? RoofOf(string kind) => kind switch { "solid" => _solid, "half" => _half, _ => null };

        // The floor's light open, under the solid roof and under the half one.
        public (float Open, float Solid, float Half) Floors() =>
            (Read(RoofOf("open")), Read(RoofOf("solid")), Read(RoofOf("half")));

        private float Read(Model? roof) => LinearMean(() => Draw(roof), Camera, Point, Width, Depth);

        public void Unload()
        {
            foreach (var model in new[] { Ground, _solid, _half }) UnloadModel(model);
        }
    }

    // The scene of the roof given, in an app opened at High.
    private static RoofScene RoofSceneOf(string roof, float height)
    {
        Open();
        if (roof != "slab") SetSceneField(4, 0.25f);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(roof switch { "slab" => new Color(200, 220, 255), "canopy" => new Color(160, 190, 240), _ => new Color(150, 190, 255) }, 1);
        var ground = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        if (roof != "slab") ground.Materials[0].Roughness = 1;
        return roof switch
        {
            "slab" => new RoofScene(roof, height, new Camera3D(new Vector3(0, 1.2f, 7), new Vector3(0, 0.2f, 0), Vector3.UnitY, 50), new Vector3(0, 0, 3.5f), 8, 4, ground),
            "canopy" => new RoofScene(roof, height, new Camera3D(new Vector3(0, 1.6f, 2), new Vector3(0, 1.2f, -6), Vector3.UnitY, 70), new Vector3(0, 0, -1), 16, 8, ground),
            _ => new RoofScene(roof, height, new Camera3D(new Vector3(0, 1.6f, 6), new Vector3(0, 1.2f, -2), Vector3.UnitY, 70), new Vector3(0, 0, 2.5f), 16, 8, ground),
        };
    }

    // The mean luminance in linear light of the frame before the bloom over the rectangle of the
    // size given about the point given, after as many frames as a capture draws.
    private static float LinearMean(Action draw, Camera3D camera, Vector3 point, int width, int height)
    {
        for (int frame = 0; frame <= SceneFieldPlan.SettleFrames + 10; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            draw();
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        var size = GetScreenWidth();
        var at = GetWorldToScreenEx(point, camera, size, GetScreenHeight());
        double sum = 0;
        for (int y = (int)at.Y - height / 2; y < (int)at.Y + height / 2; y++)
            for (int x = (int)at.X - width / 2; x < (int)at.X + width / 2; x++)
            {
                int p = (y * size + x) * 4;
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            }
        return (float)(sum / (width * height));
    }
}
