using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The top level of the window's copies as the device's rays meet them, kept while none changes.
public sealed partial class GlobalIlluminationTests
{
    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void The_Ray_Scenes_Top_Level_Is_Built_Again_The_Frame_A_Copy_Moves_Comes_Or_Goes_And_Kept_While_None_Does()
    {
        // The top level is kept while its copies' records are those its last build read, which
        // took 0.13 to 0.17 ms a frame off global illumination in Sponza at High. Kept past a copy's
        // move, coming or going, it would hold a wall where none stands, which the carried rays and
        // the reflections would meet, and leave out one that does. A green block twenty-five units
        // behind the camera, past the field's one cascade, shows in a mirror through the device's
        // rays alone, as A_Mirror_Reflects_A_Block_Past_The_Field_Through_The_Devices_Rays_At_High
        // reads it.
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.15f, 1);
        SetAmbientLight(Color.White, 0.3f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.2f, -1, 0.3f)), Color.White, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        var mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var blocks = new List<Vector3> { new(0, 1.2f, 25) };
        void Draw()
        {
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            foreach (var at in blocks)
                DrawModelEx(block, at, Vector3.UnitY, 0, new Vector3(8, 8, 1), new Color(30, 200, 40));
        }
        var camera = new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                Draw();
                EndMode3D();
                EndDrawing();
            }
        }
        float Green()
        {
            var middle = Mean(Capture(Draw), 70, 40, 20, 16);
            return middle.Y - middle.X;
        }

        Frames(20);
        var rays = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Rays;
        rays.Should().NotBeNull("High traces the device's rays here");
        var settled = rays!.TopBuilds;
        Frames(580);
        rays.TopBuilds.Should().Be(settled, "no copy moved, came or went in 580 frames");
        Green().Should().BeGreaterThan(60, "the mirror shows the block");

        void Changed(string change, bool shown)
        {
            var before = rays.TopBuilds;
            Frames(1);
            rays.TopBuilds.Should().Be(before + 1, $"the frame {change} builds the top level again");
            if (shown)
                Green().Should().BeGreaterThan(60, $"and the mirror shows the block once {change}");
            else
                Green().Should().BeLessThan(10, $"and the mirror shows no block once {change}");
        }
        blocks[0] = new Vector3(60, 1.2f, 25);
        Changed("the block moved out of the mirror's sight", false);
        blocks.Add(new Vector3(0, 1.2f, 25));
        Changed("a second block came where the first stood", true);
        blocks.RemoveAt(1);
        Changed("the second went", false);
        UnloadModel(mirror);
        UnloadModel(block);
    }
}
