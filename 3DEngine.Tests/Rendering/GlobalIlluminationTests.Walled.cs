using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Probe_Moved_Into_The_Stone_At_A_Lamps_Corner_Is_Walled_In_And_Holds_Nothing()
    {
        // A wall and a floor of stone as the voxel game meshes a section, each block's faces left
        // out where a block lies against them, and a lamp block in their corner drawn apart as an
        // emissive cube, as the game draws its lamps. The stone's faces against the lamp are left
        // out, so the field reads the stone beside the lamp as the open in front of the lamp's
        // faces, and the probe whose spot is the corner of the wall, the floor and the lamp's face
        // was moved into the stone along the field's normal, by 0.14, 0.14 and 0.23 toward the
        // stone. Its rays met 26 faces from behind of 64 and 38 in front on the development
        // machine's GPU, too few for the majority to wall it in, and it held the lamp's light,
        // which in the game the grass outside the wall took from it.
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.Low);
        var stone = new HashSet<(int X, int Y, int Z)>();
        for (int z = -4; z <= 1; z++)
        {
            for (int y = 1; y <= 3; y++) stone.Add((0, y, z));
            for (int x = 0; x <= 5; x++) stone.Add((x, 0, z));
        }
        var lamp = (X: 1, Y: 1, Z: -2);
        var section = LoadModelFromMesh(Section(stone, [lamp]));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Emissive = new Color(255, 200, 120);
        cube.Materials[0].EmissiveIntensity = 4;
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 60; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(4, 3, 1), new Vector3(1, 1, -1.5f), Vector3.UnitY, 60));
            DrawModel(section, Vector3.Zero, 1, Color.Gray);
            DrawModel(cube, new Vector3(lamp.X + 0.5f, lamp.Y + 0.5f, lamp.Z + 0.5f), 1, Color.White);
            EndMode3D();
            EndDrawing();
        }

        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var gi = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
        var plan = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;
        var origin = plan.BuiltOrigin(0)!.Value;
        var spacing = plan.CellOf(0) * GlobalIlluminationRenderer.ProbeSpacing;
        var corner = new Vector3(1, 1, -1);
        var at = (corner - origin) / spacing - new Vector3(0.5f);
        at.Should().Be(new Vector3(MathF.Round(at.X), MathF.Round(at.Y), MathF.Round(at.Z)), "the corner is a probe's spot, as the game's corners of blocks are");
        var (p, n) = (gi.Probes, gi.Texels[0]);
        var (px, py, pz) = ((int)at.X, (int)at.Y, (int)at.Z);
        var merged = ((GraphicsDevice)renderer.Context.Graphics!).ReadProbeVolume(gi, 0, merged: true);
        var side = p * n;
        var alpha = merged[((pz * side + py * n) * side + px * n) * 4 + 3];
        alpha.Should().BeLessThan(0.25f, "a probe a quarter of whose rays meet faces from behind stands inside a solid and holds nothing");
        UnloadModel(section);
        UnloadModel(cube);
    }

    // The faces of the blocks given, a unit cube each at its corner, where no block of their own or
    // of those drawn apart lies against them.
    private static ModelMesh Section(HashSet<(int X, int Y, int Z)> blocks, (int X, int Y, int Z)[] apart)
    {
        (int X, int Y, int Z)[] ways = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];
        var vertices = new List<ModelVertex>();
        var indices = new List<uint>();
        foreach (var block in blocks)
            foreach (var way in ways)
            {
                var next = (block.X + way.X, block.Y + way.Y, block.Z + way.Z);
                if (blocks.Contains(next) || apart.Contains(next)) continue;
                var normal = new Vector3(way.X, way.Y, way.Z);
                var middle = new Vector3(block.X + 0.5f, block.Y + 0.5f, block.Z + 0.5f) + normal * 0.5f;
                var u = Math.Abs(way.Y) == 1 ? Vector3.UnitX : Vector3.UnitY;
                var v = Vector3.Cross(normal, u);
                var first = (uint)vertices.Count;
                foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    vertices.Add(new ModelVertex(middle + (u * a + v * b) * 0.5f, normal, Vector2.Zero));
                indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
            }
        return UploadMesh([.. vertices], [.. indices]);
    }
}
