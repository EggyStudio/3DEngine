using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A surface that lets a share of global illumination's light through, as a canopy's leaves.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Floor_Under_A_Slab_That_Lets_Light_Through_Takes_The_Sky_Light_Its_Share_Lets_Through(GlobalIllumination quality)
    {
        // A gray floor lit by the sky alone, read in linear light under a dark green slab a unit
        // thick and twenty across, two units over it, with no slab, with a solid one, and with one
        // letting half the light through a unit and one letting all of it through, as the voxel
        // game's rain forest floor under its canopy took none of the sky's light where the field held
        // the leaves solid, at Low through the cascades' rays and at High through the screen's
        // probes' too. A ray keeps half its light a unit of slab, and one at a slant crosses more of
        // it, so the half slab lets a quarter or so of the sky's light the solid one holds back reach
        // the floor, 0.23 at Low and 0.29 at High as this test measured on the development machine's
        // GPU, where a slant's depth over the floor's sky makes it 0.26 to 0.30, and a slab letting
        // all of it through 0.95 and 0.98.
        Open();
        SetGlobalIllumination(quality);
        SetAmbientLight(new Color(200, 220, 255), 1);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 1.2f, 7), new Vector3(0, 0.2f, 0), Vector3.UnitY, 50);
        float Floor(Model? roof)
        {
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(128, 128, 128));
                if (roof is { } model) DrawModelEx(model, new Vector3(0, 2.5f, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
            }, camera);
            var at = GetWorldToScreenEx(new Vector3(0, 0, 3.5f), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 4, (int)at.Y - 2, 8, 4) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }
        Model Leaves(float through)
        {
            var model = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            model.Materials[0].LightThrough = through;
            return model;
        }

        var open = Floor(null);
        var solid = Floor(slab);
        var (halfway, allway) = (Leaves(0.5f), Leaves(1));
        var half = Floor(halfway);
        var all = Floor(allway);
        (open - solid).Should().BeGreaterThan(0.1f, $"a solid slab keeps the sky's light off the floor under it, {solid:0.000} against {open:0.000}");
        ((all - solid) / (open - solid)).Should().BeGreaterThan(0.9f, $"a slab letting all of it through holds back next to none, {all:0.000}");
        ((half - solid) / (open - solid)).Should().BeInRange(0.15f, 0.4f,
            $"a slab letting half through a unit lets a quarter or so of the sky's light it would hold back through, {half:0.000} between {solid:0.000} and {open:0.000}");
        foreach (var model in new[] { slab, halfway, allway }) UnloadModel(model);
    }
}
