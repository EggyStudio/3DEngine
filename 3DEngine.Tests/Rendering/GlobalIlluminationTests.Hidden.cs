using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The foot of a wall whose floor the world's probes under it see lit from below, which those
// probes, hidden from the foot, lend none of.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Foot_Of_A_Wall_Takes_None_Of_The_Light_The_Hidden_Probes_Under_Its_Floor_See()
    {
        // The feet test's closed room of rough gray slabs two cells thick lit by a dim lamp, in a sky
        // 64 times as bright that lights its floor's underside, at Low, where every surface reads the
        // world's probes. At the twentieth of their weight a hidden probe kept, the probes under
        // the floor lent the wall's foot the underside's light and lit it brighter than the wall
        // above it, which with them left out it is not.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        SetAmbientLight(Color.White, 64);
        CreatePointLight(new Vector3(0, 2.2f, 0), new Color(255, 240, 220), 1, range: 8);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 1;
        var gray = new Color(150, 150, 150);
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(4.6f, 0.3f, 4.6f), gray);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(4.6f, 0.3f, 4.6f), gray);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2.15f), Vector3.UnitY, 0, new Vector3(4.6f, 3, 0.3f), gray);
            DrawModelEx(slab, new Vector3(0, 1.5f, 2.15f), Vector3.UnitY, 0, new Vector3(4.6f, 3, 0.3f), gray);
            DrawModelEx(slab, new Vector3(-2.15f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4.6f), gray);
            DrawModelEx(slab, new Vector3(2.15f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4.6f), gray);
        }
        var camera = new Camera3D(new Vector3(-0.6f, 1.1f, 1.7f), new Vector3(2.0f, 0.3f, -1.2f), Vector3.UnitY, 60);
        var image = Capture(Draw, camera);

        // The right wall a hand above the floor and a unit up, at four places along it.
        float Read(Vector3 point)
        {
            var at = GetWorldToScreenEx(point, camera, image.Width, image.Height);
            return Mean(image, (int)at.X - 2, (int)at.Y - 1, 4, 3).Y;
        }
        var along = new[] { -1.2f, -0.6f, 0f, 0.6f };
        var foot = along.Average(z => Read(new Vector3(2.0f, 0.06f, z)));
        var wall = along.Average(z => Read(new Vector3(2.0f, 1.0f, z)));
        foot.Should().BeLessThan(wall, $"the probes under the floor lend the wall's foot none of the underside's light, {foot:0.0} where the wall above it reads {wall:0.0}");
        UnloadModel(slab);
    }
}
