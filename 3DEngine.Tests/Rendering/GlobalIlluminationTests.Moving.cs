using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// An emissive mesh that moves, too large to be carried as a glow light, whose box the field is
// stamped with each frame, its emission among it.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void An_Emissive_Bar_Too_Long_To_Be_A_Glow_Light_Lights_A_Wall_As_It_Moves_As_It_Does_Held_Still(GlobalIllumination quality)
    {
        // A gray wall and floor in the dark, and before the wall an emissive bar five units long,
        // past the 2.4 a glow light may be at cells of 0.15, held still, so the field is built with
        // it, or moved a little each frame, so it never settles and is stamped as its box. Stamped
        // with its color alone, the moving bar lit nothing past itself and left the wall dark, so
        // its box carries its emission.
        Open();
        SetGlobalIllumination(quality);
        var (slab, bar) = (LoadModelFromMesh(GenMeshCube(1, 1, 1)), LoadModelFromMesh(GenMeshCube(1, 1, 1)));
        bar.Materials[0].Emissive = new Color(255, 140, 40);
        bar.Materials[0].EmissiveIntensity = 4;
        var camera = new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50);
        float Wall(bool moving)
        {
            var frame = 0;
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.25f, 0), Vector3.UnitY, 0, new Vector3(10, 0.5f, 6), Color.Gray);
                DrawModelEx(slab, new Vector3(0, 2, -1.25f), Vector3.UnitY, 0, new Vector3(10, 4, 0.5f), Color.Gray);
                var x = moving ? 0.05f * (frame++ % 2 == 0 ? 1 : -1) : 0;
                DrawModelEx(bar, new Vector3(x, 0.6f, 0), Vector3.UnitY, 0, new Vector3(5, 0.3f, 0.3f), Color.White);
            }, camera);
            // The wall above the bar, across its middle.
            var at = GetWorldToScreenEx(new Vector3(0, 1.6f, -1), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 8, (int)at.Y - 2, 16, 4) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }

        var still = Wall(moving: false);
        var moving = Wall(moving: true);
        still.Should().BeGreaterThan(0.01f, "the bar held still lights the wall");
        (moving / still).Should().BeInRange(0.6f, 1.8f, $"the moving bar lights it as the still one does, {moving:0.0000} against {still:0.0000} at {quality}");
        foreach (var model in new[] { slab, bar }) UnloadModel(model);
    }
}
