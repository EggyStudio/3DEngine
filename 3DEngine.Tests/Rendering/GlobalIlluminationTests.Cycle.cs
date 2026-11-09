using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The cycle of sixteen places a carried probe's rays move through, kept where they move.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.Medium)]
    [InlineData(GlobalIllumination.High)]
    public void A_Carried_Probes_Cycle_Is_Kept_Whole_Only_Where_Its_Rays_Move(GlobalIllumination quality)
    {
        // A carried probe's rays move through sixteen places and meet the meshes at High on a
        // device that traces rays, its cycle's six images a cascade made whole there in every
        // cascade but the last counted and the far, 9 MB at Sponza's High, and stand-ins of a texel
        // elsewhere, a mean of one place being that place, so Low, Medium and a device that traces
        // no rays, as lavapipe and MoltenVK, keep none of it. Under a slab two and a half units up
        // in four cascades the probes beneath it see none of those above and carry their rays on,
        // and each cascade they carry in has its cycle whole, so a trace that came to carry in a
        // cascade whose cycle is a stand-in would read one place for sixteen.
        var block = UnderSlab(quality, 120);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var moving = quality == GlobalIllumination.High && device.CanQueryRays;
        var carried = GlobalIlluminationStages.Of(device).ReadCarriedMoving(probes);
        probes.CycleBytes.Should().Be(moving ? probes.Texels.Take(probes.CycleCascades).Sum(n => 6L * 8 * (probes.Probes * n) * (probes.Probes * n) * probes.Probes) : 0,
            $"the cycle is kept whole at {quality} only where the rays move, {(moving ? "here" : "not here")}");
        if (moving)
            carried.Sum().Should().BeGreaterThan(0, "the probes under the slab carry their rays on");
        for (int c = 0; c < probes.Cascades; c++)
            if (carried[c] > 0)
                c.Should().BeLessThan(probes.CycleCascades, $"cascade {c} carries {carried[c]} probes with their rays moving, so its cycle is whole");
        SceneFieldCommands.IlluminationState(GetApp().World).Should().Contain("world probes");
        UnloadModel(block);
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Carried_Probes_Share_Of_The_Sky_Is_Kept_Through_Its_Cycle_As_Its_Light_Is()
    {
        // Under the slab the carried probes' rays move through their sixteen places, some of them
        // past its edge to the sky and some onto its underside, so a share of the sky and a mark of
        // what the rays met read from each frame's places alone moved with them, a probe below
        // taking the sky through a texel above in some frames and not others, and every probe whose
        // hits read them flickered, the edges of Sponza's balcony 0.16% of the frame's pixels by 12
        // levels a frame. Kept as the light is, every texel's share holds still from frame to frame
        // once its cycle is whole.
        var block = UnderSlab(GlobalIllumination.High, 120);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        GlobalIlluminationStages.Of(device).ReadCarriedMoving(probes).Take(probes.CycleCascades).Sum().Should().BeGreaterThan(0, "the probes under the slab carry their rays on");
        var before = Enumerable.Range(0, probes.CycleCascades).Select(c => GlobalIlluminationStages.Of(device).ReadMergedSky(probes, c)).ToArray();
        var most = 0f;
        for (int frame = 0; frame < 32; frame++)
        {
            UnderSlab(GlobalIllumination.High, 1, block);
            for (int c = 0; c < probes.CycleCascades; c++)
            {
                var now = GlobalIlluminationStages.Of(device).ReadMergedSky(probes, c);
                for (int i = 0; i < now.Length; i++)
                    most = Math.Max(most, Math.Abs(now[i] - before[c][i]));
                before[c] = now;
            }
        }
        most.Should().BeLessThan(1e-3f, "a carried probe's share of the sky is its whole cycle's, the same each frame");
        UnloadModel(block);
    }

    // A ground 30 units wide and a slab 20 wide two and a half units over it, under the light from
    // all around, drawn so many frames, the window opened at the quality first.
    private Model UnderSlab(GlobalIllumination quality, int frames, Model? drawn = null)
    {
        if (drawn is not { } block)
        {
            Open();
            SetSceneField(4, 0.15f, 2);
            SetGlobalIllumination(quality);
            SetAmbientLight(new Color(200, 220, 255), 1);
            block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        }
        var camera = new Camera3D(new Vector3(0, 1.2f, 7), new Vector3(0, 0.2f, 0), Vector3.UnitY, 50);
        for (int frame = 0; frame < frames; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(30, 1, 30), Color.Gray);
            DrawModelEx(block, new Vector3(0, 2.5f, 0), Vector3.UnitY, 0, new Vector3(20, 1, 20), new Color(60, 90, 50));
            EndMode3D();
            EndDrawing();
        }
        return block;
    }
}
