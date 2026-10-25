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

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Two_Blocks_Meeting_Along_An_Edge_Close_It_To_A_Glow_Lights_Light()
    {
        // The voxel game's room of stone whose wall steps a block east halfway, its two halves
        // meeting along one edge alone, with a glowstone west of the edge and the eye east of it, so
        // no ray from the glowstone reaches what the eye sees but through the edge itself. The
        // field's cells about the edge lay inside the blocks on one diagonal and outside them on the
        // other, the distance between them never below zero along the edge, and the march from the
        // floor to the glowstone saw half of it through the edge, the floor about the edge's foot 10
        // sRGB levels at 640 by 360 at High on the development machine's GPU and 7 with the edge
        // closed by a block, where a path-traced reference reads none. With the cells about such an
        // edge held inside it reads 2 either way.
        var config = Config.Default.WithWindow("gi test", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        GetApp().World.Resource<GlobalIlluminationSettings>().ScreenOff = true;
        SetAmbientLight(Color.Black, 0);
        var glowstone = (X: 15, Y: 5, Z: -14);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Emissive = new Color(255, 200, 120);
        cube.Materials[0].EmissiveIntensity = 4;
        // The game's eye 1.62 over the block it stands on, facing 225 degrees and 32 down.
        var eye = new Vector3(21.2f, 6.62f, -20.2f);
        float heading = 225 * MathF.PI / 180, pitch = -32 * MathF.PI / 180;
        var camera = new Camera3D(eye, eye + new Vector3(MathF.Sin(heading) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(heading) * MathF.Cos(pitch)), Vector3.UnitY, 70);
        int Brightest(bool closed)
        {
            // voxel.room 10 4 -22 22 11 -10 stone, a shell a block thick, and the wall's halves.
            var stone = new HashSet<(int X, int Y, int Z)>();
            for (int x = 10; x <= 22; x++)
                for (int y = 4; y <= 11; y++)
                    for (int z = -22; z <= -10; z++)
                        if (x is 10 or 22 || y is 4 or 11 || z is -22 or -10)
                            stone.Add((x, y, z));
            for (int y = 5; y <= 10; y++)
            {
                for (int z = -21; z <= -16; z++) stone.Add((16, y, z));
                for (int z = -15; z <= -11; z++) stone.Add((17, y, z));
                if (closed) stone.Add((16, y, -15));
            }
            var section = LoadModelFromMesh(Section(stone, [glowstone]));
            var frame = Capture(() =>
            {
                DrawModel(section, Vector3.Zero, 1, new Color(130, 130, 130));
                DrawModel(cube, new Vector3(glowstone.X + 0.5f, glowstone.Y + 0.5f, glowstone.Z + 0.5f), 1, new Color(181, 154, 87));
            }, camera);
            UnloadModel(section);
            // The brightest pixel about the edge's foot, where the light through the edge fanned
            // over the floor.
            var foot = GetWorldToScreenEx(new Vector3(17, 5, -15), camera, frame.Width, frame.Height);
            var most = 0;
            for (int y = Math.Max(0, (int)foot.Y - 50); y < Math.Min(frame.Height, (int)foot.Y + 150); y++)
                for (int x = Math.Max(0, (int)foot.X - 125); x < Math.Min(frame.Width, (int)foot.X + 125); x++)
                {
                    var c = GetImageColor(frame, x, y);
                    most = Math.Max(most, (c.R * 299 + c.G * 587 + c.B * 114) / 1000);
                }
            return most;
        }

        var (open, shut) = (Brightest(false), Brightest(true));
        open.Should().BeLessThanOrEqualTo(shut + 2, $"no light passes the edge the wall's halves meet along, the brightest about its foot {open} levels with it open and {shut} with it closed");
        open.Should().BeLessThanOrEqualTo(4, "the floor about the edge's foot is as dark as a path-traced reference's, but for a level or two");
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
