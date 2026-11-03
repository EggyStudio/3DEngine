using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The floor of a court open to the sky between two roofed galleries, narrower than the coarsest
// cascade's probes stand apart, which takes the sky through the court's opening.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Floor_Of_A_Court_Narrower_Than_The_Probes_Apart_Takes_The_Sky_Through_Its_Opening()
    {
        // A gray floor lit by the sky alone at the middle of a court 5 units wide between two
        // galleries roofed 6 units up, in a field of four cascades of cells a quarter of a unit,
        // the last's probes 16 units apart, against the same floor in the open. A long slot of
        // that width at that height gives the floor the sky over a view factor of sin 22.6
        // degrees, 0.38. With the light past the probes above read from them alone, which stood
        // under the roofs and saw none of the sky up the court, the floor took 0.223 of the open
        // floor's light on the development machine's GPU, and with the rays those probes see no
        // sky along traced on to the sky past them, 0.297.
        Open();
        SetSceneField(4, 0.25f);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(160, 190, 240), 1);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        var gray = new Color(128, 128, 128);
        var camera = new Camera3D(new Vector3(0, 1.6f, 6), new Vector3(0, 0.6f, -2), Vector3.UnitY, 70);
        float Floor(bool galleries)
        {
            var image = Capture(() =>
            {
                DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(80, 1, 80), gray);
                if (galleries)
                    foreach (var side in new[] { -1f, 1f })
                    {
                        // A gallery on each side, its roof 6 deep and its outer wall closing it.
                        DrawModelEx(slab, new Vector3(side * 5.5f, 6.25f, 0), Vector3.UnitY, 0, new Vector3(6, 0.5f, 24), gray);
                        DrawModelEx(slab, new Vector3(side * 8.75f, 3, 0), Vector3.UnitY, 0, new Vector3(0.5f, 6, 24), gray);
                    }
            }, camera);
            var at = GetWorldToScreenEx(new Vector3(0, 0, 0), camera, image.Width, image.Height);
            var c = Mean(image, (int)at.X - 8, (int)at.Y - 4, 16, 8) / 255;
            return 0.2126f * MathF.Pow(c.X, 2.2f) + 0.7152f * MathF.Pow(c.Y, 2.2f) + 0.0722f * MathF.Pow(c.Z, 2.2f);
        }
        var open = Floor(false);
        var court = Floor(true);
        (court / open).Should().BeGreaterThan(0.26f, $"the court's floor takes the sky up its opening, {court:0.0000} where the open floor takes {open:0.0000}");
        UnloadModel(slab);
    }
}
