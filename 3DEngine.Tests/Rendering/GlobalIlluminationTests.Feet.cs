using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The foot of a rough wall standing on a floor whose probes under it see the sky, which the wall's
// reflection along its mirror way down to the floor takes none of.
public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Foot_Of_A_Rough_Wall_Reflects_None_Of_The_Sky_The_Probes_Under_Its_Floor_See()
    {
        // A closed room of rough gray slabs two cells thick lit by a dim lamp, standing in a sky 64
        // times as bright that the probes under its floor see, the camera inside looking along its
        // right wall. On the development machine's GPU the wall's foot read 79.7 to 84.8 of 255
        // where the wall above it reads 66.5 to 72.7, the probes under the floor lending the foot's
        // reflection the sky, and 60.2 to 62.5 with them left out.
        Open();
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.White, 64);
        CreatePointLight(new Vector3(0, 2.2f, 0), new Color(255, 240, 220), 1, range: 8);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0].Roughness = 0.4f;
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
        foot.Should().BeLessThan(wall, $"the wall's foot reflects none of the sky under the floor, {foot:0.0} where the wall above it reads {wall:0.0}");
        UnloadModel(slab);
    }
}
