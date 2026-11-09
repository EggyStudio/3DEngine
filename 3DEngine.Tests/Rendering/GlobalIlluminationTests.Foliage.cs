using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A surface that lets a share of global illumination's light through, as a canopy's leaves.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low, 2.5f, 0.33f)]
    [InlineData(GlobalIllumination.High, 2.5f, 0.34f)]
    [InlineData(GlobalIllumination.Low, 5.5f, 0.38f)]
    [InlineData(GlobalIllumination.High, 5.5f, 0.35f)]
    public void A_Floor_Under_A_Slab_That_Lets_Light_Through_Takes_The_Sky_Light_Its_Share_Lets_Through(GlobalIllumination quality, float height, float least)
    {
        // A gray floor lit by the sky alone, read in linear light under a dark green slab a unit
        // thick and twenty across, two units over it or five, with no slab, with a solid one, and
        // with one letting half the light through a unit and one letting all of it through, as the
        // voxel game's rain forest floor under its canopy took none of the sky's light where the
        // field held the leaves solid, at Low through the cascades' rays and at High through the
        // screen's probes' too. A ray keeps half its light a unit of slab, and one at a slant
        // crosses more of it, so the half slab lets the share of the sky's light the solid one
        // holds back reach the floor that the slab's depth along each way the floor's sky comes
        // from gives, 0.36 two units up and 0.41 five, and a slab letting all of it through lets
        // nearly all of it reach the floor. Each crossing is taken once, by the cascade whose
        // interval it is entered in, since the cascades' overlapping intervals cross the slab
        // twice where it straddles one's end, and the merge sees the probes above past the leaves,
        // without which the floor took too little, least of all five up at High, past the screen's
        // probes' reach.
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
                if (roof is { } model) DrawModelEx(model, new Vector3(0, height, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
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
        ((half - solid) / (open - solid)).Should().BeInRange(least, 0.48f,
            $"a slab letting half through a unit lets a third or more of the sky's light it would hold back through, {half:0.000} between {solid:0.000} and {open:0.000}");
        foreach (var model in new[] { slab, halfway, allway }) UnloadModel(model);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low, 2.5f)]
    [InlineData(GlobalIllumination.High, 2.5f)]
    [InlineData(GlobalIllumination.High, 5.5f)]
    public void A_Floor_Under_A_Slab_Letting_All_Light_Through_Reflects_The_Sky_As_In_The_Open(GlobalIllumination quality, float height)
    {
        // The same floor under a sky map, whose reflection the probes' share of the sky along the
        // mirror way scales, which the last cascade's rays counted none of past the slab, having
        // marked the leaves they crossed as a surface, so the floor under a slab letting all the
        // light through took less of what the solid slab held back than the slab let through.
        Open();
        SetGlobalIllumination(quality);
        SetEnvironmentMap(GenImageColor(64, 32, new Color(200, 220, 255)));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 1.2f, 7), new Vector3(0, 0.2f, 0), Vector3.UnitY, 50);
        float Floor(Model? roof)
        {
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(128, 128, 128));
                if (roof is { } model) DrawModelEx(model, new Vector3(0, height, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
            }, camera);
            var at = GetWorldToScreenEx(new Vector3(0, 0, 3.5f), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 4, (int)at.Y - 2, 8, 4) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }
        var allway = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        allway.Materials[0].LightThrough = 1;

        var open = Floor(null);
        var solid = Floor(slab);
        var all = Floor(allway);
        ((all - solid) / (open - solid)).Should().BeGreaterThan(0.9f, $"a slab letting all of it through holds back next to none, {all:0.000} between {solid:0.000} and {open:0.000}");
        foreach (var model in new[] { slab, allway }) UnloadModel(model);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Floor_Under_Leaves_Past_The_Screen_Probes_Reach_Takes_The_Sky_Light_They_Let_Through()
    {
        // A green floor under a canopy thirty units across, a unit thick and five over it, past
        // the four units the screen's probes reach at High, read with no canopy, with a solid one
        // and with one letting half the light through a unit. The probes of each cascade under the
        // canopy took no light from those of the cascade above while the canopy hid them from them,
        // so the screen's probes read the cascades' light past their reach as black, and the floor
        // took next to none of the sky's light the solid canopy holds back, where the canopy's
        // depth along each way gives 0.38. The merge sees past the leaves (seesPastLeaves) and each
        // crossing is taken once by one cascade.
        Open();
        SetSceneField(4, 0.25f);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(150, 190, 255), 1);
        var ground = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        ground.Materials[0].Roughness = 1;
        var camera = new Camera3D(new Vector3(0, 1.6f, 6), new Vector3(0, 1.2f, -2), Vector3.UnitY, 70);
        float Floor(Model? roof)
        {
            var image = Capture(() =>
            {
                DrawModelEx(ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(80, 1, 80), new Color(90, 140, 60));
                if (roof is { } model) DrawModelEx(model, new Vector3(0, 5.5f, -4), Vector3.UnitY, 0, new Vector3(30, 1, 30), new Color(50, 110, 40));
            }, camera);
            var at = GetWorldToScreenEx(new Vector3(0, 0, 2.5f), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 8, (int)at.Y - 4, 16, 8) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }
        Model Leaves(float through)
        {
            var model = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            model.Materials[0].Roughness = 1;
            model.Materials[0].LightThrough = through;
            return model;
        }

        var (solidRoof, halfway) = (Leaves(0), Leaves(0.5f));
        var open = Floor(null);
        var solid = Floor(solidRoof);
        var half = Floor(halfway);
        ((half - solid) / (open - solid)).Should().BeGreaterThan(0.33f,
            $"the leaves let the sky's light through to the floor past the screen's probes' reach, {half:0.0000} between {solid:0.0000} and {open:0.0000}");
        foreach (var model in new[] { ground, solidRoof, halfway }) UnloadModel(model);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low, 2.5f, 0.11f, 0.2f)]
    [InlineData(GlobalIllumination.High, 2.5f, 0.14f, 0.23f)]
    [InlineData(GlobalIllumination.High, 5.5f, 0.18f, 0.28f)]
    public void A_Floor_Under_A_Canopy_Of_Leaf_Blocks_Takes_The_Sky_Light_Their_Faces_Let_Through(GlobalIllumination quality, float height, float least, float most)
    {
        // A gray floor lit by the sky alone under a canopy of leaf blocks twenty a side and two
        // deep, each a cube of its own, so two faces stand between neighbors as a voxel game's
        // cut-out leaves have them, read with no canopy, a solid one and one letting half the light
        // through a unit. A ray crosses a face a block, up to four, the first face it crosses
        // shaded for all of them, which reads as each face shaded where the ray crossed it does. A
        // path-traced reference takes 0.17 of the sky's light the solid canopy holds back two and
        // a half units under it and 0.21 five and a half under it, at the point read three units
        // before the eye in the finest cascade's middle, and the bounds lie around those. The
        // floor under the solid canopy sees the sky below the canopy's edges, 5 units off on its
        // near side, which a sky even over the directions below those edges gives about a twelfth
        // of the open floor's light, and read from the probes above alone it was dark, so the rays
        // those probes see no sky along are traced on to the sky past them.
        Open();
        SetSceneField(4, 0.25f);
        SetGlobalIllumination(quality);
        SetAmbientLight(new Color(160, 190, 240), 1);
        var ground = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        ground.Materials[0].Roughness = 1;
        // The eye stands three units from the floor's point read, which so lies in the finest
        // cascade's middle around the eye.
        var camera = new Camera3D(new Vector3(0, 1.6f, 2), new Vector3(0, 1.2f, -6), Vector3.UnitY, 70);
        float Floor(Model? roof)
        {
            var image = Capture(() =>
            {
                DrawModelEx(ground, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(80, 1, 80), new Color(128, 128, 128));
                if (roof is { } model)
                    for (int x = -10; x < 10; x++)
                        for (int z = -16; z < 4; z++)
                            for (int y = 0; y < 2; y++)
                                DrawModel(model, new Vector3(x + 0.5f, height + y, z + 0.5f), 1, new Color(60, 110, 45));
            }, camera);
            var at = GetWorldToScreenEx(new Vector3(0, 0, -1), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 8, (int)at.Y - 4, 16, 8) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }
        Model Leaves(float through)
        {
            var model = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            model.Materials[0].Roughness = 1;
            model.Materials[0].LightThrough = through;
            return model;
        }

        var (solidRoof, halfway) = (Leaves(0), Leaves(0.5f));
        var open = Floor(null);
        var solid = Floor(solidRoof);
        var half = Floor(halfway);
        ((half - solid) / (open - solid)).Should().BeInRange(least, most,
            $"the leaves let the sky's light through to the floor, {half:0.0000} between {solid:0.0000} and {open:0.0000}");
        foreach (var model in new[] { ground, solidRoof, halfway }) UnloadModel(model);
    }
}
