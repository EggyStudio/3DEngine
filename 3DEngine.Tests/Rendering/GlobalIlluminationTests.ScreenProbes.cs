using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Global illumination with the screen's probes left out, the cascades' light read alone and the
// glow lights read from probes standing for them alone.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Frame_With_The_Screens_Probes_Left_Out_Draws_The_Cascades_Light_Alone()
    {
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }
        // The cascades read alone through the toggle, which traces the screen's probes and leaves
        // them unread, then with the probes left out, then with no global illumination.
        SetGlobalIllumination(GlobalIllumination.Low);
        var gi = GetApp().World.Resource<GlobalIlluminationSettings>();
        gi.ScreenOff = true;
        var unread = Capture(Draw);
        gi.ScreenOff = false;
        SetGlobalIlluminationScreenProbes(false);
        var left = Capture(Draw);
        GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen.Should().BeNull("the screen's probes are neither made nor traced");
        SetGlobalIllumination(GlobalIllumination.Off);
        var still = Capture(Draw);

        var differing = 0;
        for (int y = 0; y < left.Height; y++)
            for (int x = 0; x < left.Width; x++)
                if (GetImageColor(left, x, y) != GetImageColor(unread, x, y)) differing++;
        differing.Should().Be(0, "the frame reads the cascades' light as the toggle's does");
        var lit = Mean(left, 58, 48, 6, 10);
        var dark = Mean(still, 58, 48, 6, 10);
        (lit.X - dark.X).Should().BeGreaterThan(20, $"the cascades bring the wall's light to the block's side, {lit} against {dark}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void With_The_Screens_Probes_Left_Out_A_Glow_Light_Is_Read_From_Probes_Standing_For_It_Alone()
    {
        // The strip's room, the strip carried as a glow light. With the screen's probes left out,
        // probes stand for the glow lights alone and a pixel reads the texels around it, which
        // gives the floor the light a pixel marching to every light itself gives it, the screen's
        // probes traced and left unread through the toggle, and with no glow light none are made.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        strip.Materials[0].Emissive = new Color(255, 230, 190);
        strip.Materials[0].EmissiveIntensity = 30;
        var camera = new Camera3D(new Vector3(0, 1.4f, 2.3f), new Vector3(0, 1.2f, -2.5f), Vector3.UnitY, 65);
        void Room()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2.65f), Vector3.UnitY, 0, new Vector3(5.6f, 3, 0.3f), Color.White);
            DrawModelEx(slab, new Vector3(-2.65f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 5.6f), Color.White);
            DrawModelEx(slab, new Vector3(2.65f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 5.6f), Color.White);
            DrawModelEx(strip, new Vector3(0, 1.5f, -2.45f), Vector3.UnitY, 0, new Vector3(2, 0.06f, 0.06f), Color.White);
        }
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var renderer = GetApp().World.Resource<Engine.Renderer>();

        settings.ScreenOff = true;
        var marched = Capture(Room, camera);
        settings.ScreenOff = false;
        SetGlobalIlluminationScreenProbes(false);
        var read = Capture(Room, camera);
        var gi = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!;
        gi.GlowOnly(renderer.RenderWorld).Should().BeTrue("the strip is carried as a glow light");
        gi.Screen.Should().NotBeNull("probes stand for the glow lights alone");

        var (byMarching, byReading) = (Mean(marched, 0, 60, 160, 36), Mean(read, 0, 60, 160, 36));
        byReading.X.Should().BeGreaterThan(40, $"the strip lights the floor, {byReading}");
        Vector3.Distance(byMarching, byReading).Should().BeLessThan(6, $"as a pixel marching to it does, {byReading} against {byMarching}");

        settings.GlowOff = true;
        Capture(Room, camera);
        gi.Screen.Should().BeNull("with no glow light the screen's probes left out are not made at all");
        UnloadModel(slab);
        UnloadModel(strip);
    }
}
