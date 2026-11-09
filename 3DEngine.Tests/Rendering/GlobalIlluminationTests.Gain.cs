using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The gain SetBounceGain takes the light past the first bounce times.
public sealed partial class GlobalIlluminationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void The_Bounce_Gain_Is_One_Unless_Set_And_Never_Below_Zero()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        GetBounceGain().Should().Be(1);
        SetBounceGain(1.5f);
        GetBounceGain().Should().Be(1.5f);
        GetApp().World.Resource<GlobalIlluminationSettings>().Quality.Should().Be(GlobalIllumination.Off, "the gain asks for no global illumination");
        SetBounceGain(-2);
        GetBounceGain().Should().Be(0);
        SetBounceGain(float.NaN);
        GetBounceGain().Should().Be(1);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Floor_Lit_By_A_Panel_Takes_More_Of_Its_Walls_Light_As_The_Bounce_Gain_Rises()
    {
        // The emissive panel's room of gray walls, whose floor takes the panel's light straight and
        // the walls' light after it, which the gain takes times itself.
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
        SetGlobalIllumination(GlobalIllumination.Low);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();

        // The floor at the room's middle, near the bottom of the picture, at each gain.
        float Floor(Image image) => Mean(image, 60, 80, 40, 8).Y;
        settings.AgainOff = true;
        var once = Floor(Capture(Draw));
        settings.AgainOff = false;
        var at = new Dictionary<float, float>();
        foreach (var gain in new[] { 0f, 1f, 2f })
        {
            SetBounceGain(gain);
            at[gain] = Floor(Capture(Draw));
        }

        at[0].Should().BeApproximately(once, 1, "a gain of 0 leaves the light past the first bounce out as gi.toggle again off does");
        at[1].Should().BeGreaterThan(at[0] + 2, $"the walls' light reaches the floor past the panel's, {at[0]} to {at[1]}");
        at[2].Should().BeGreaterThan(at[1] + 2, $"and twice as much of it at a gain of 2, {at[1]} to {at[2]}");
        UnloadModel(slab);
        UnloadModel(panel);
    }
}
