using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Double-sided faces thinner than a cell, kept by their planes in the cells they are nearest, so a
// march meets one from either side wherever it lies between the cells' middles and two of them two
// cells apart leave the cells between them open.
public sealed partial class SceneFieldTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Double_Sided_Sheet_Thinner_Than_A_Cell_Is_Met_At_Its_Plane_Wherever_It_Lies_Between_The_Cells()
    {
        var config = Config.Default.WithWindow("scene field sheets", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var sheet = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        sheet.Materials[0].DoubleSided = true;
        var camera = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        // A sheet exactly half way between two rows of cells, where its distance in both is half a
        // cell, one a fifth of a cell nearer the row above, as a floor of an imported room is, and
        // two sheets two cells apart, as the voxel game's crops stand.
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            BeginMode3D(camera);
            DrawModel(sheet, new Vector3(-2.5f, 0, 0), 1, Color.Gray);
            DrawModel(sheet, new Vector3(2.5f, 1.05f, 0), 1, Color.Gray);
            DrawModel(sheet, new Vector3(-2.5f, 3, 0), 1, Color.Gray);
            DrawModel(sheet, new Vector3(-2.5f, 3.5f, 0), 1, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var (distances, sheets) = (device.ReadSceneField(fields.Field!), device.ReadSceneFieldSheets(fields.Field!));
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.25f;
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;

        // A point's distance as a march reads it, the cells' distances blended up the column, the
        // sheets lying level, or the plane the cell it lies in keeps where nearer, as
        // fieldDistanceIn takes them.
        float At(float x, float y)
        {
            int column = (int)((x - origin.X) / cell), row = (int)((y - origin.Y) / cell), depth = (int)((0 - origin.Z) / cell);
            float Middle(int r) => distances[(depth * size + r) * size + column];
            var along = (y - origin.Y) / cell - 0.5f;
            var below = (int)MathF.Floor(along);
            var blended = Middle(below) + (Middle(below + 1) - Middle(below)) * (along - below);
            int index = (depth * size + row) * size + column;
            var middle = origin + (new Vector3(column, row, depth) + new Vector3(0.5f)) * cell;
            var kept = new Vector3(sheets[index * 3], sheets[index * 3 + 1], sheets[index * 3 + 2]);
            var plane = kept.Length() < 0.5f ? float.MaxValue
                : MathF.Abs(Vector3.Dot(new Vector3(x, y, 0) - middle, Vector3.Normalize(kept)) - (kept.Length() - 2) * cell) - 0.25f * cell;
            return MathF.Min(blended, plane);
        }

        // Each sheet is met at its plane from the row of cells above it and the row below, which
        // the cells' blend holds no nearer than half a cell where it lies between them.
        foreach (var (x, height) in new[] { (-2.5f, 0f), (2.5f, 1.05f), (-2.5f, 3f), (-2.5f, 3.5f) })
        {
            At(x, height + cell * 0.01f).Should().BeLessThan(cell * 0.1f, $"the sheet at {height} is met from above at its plane");
            At(x, height - cell * 0.01f).Should().BeLessThan(cell * 0.1f, $"the sheet at {height} is met from below at its plane");
        }
        // Between the two sheets two cells apart every point lies half a cell or more from both as a
        // march reads it, where held below zero within half a cell either side every cell between
        // them read a surface.
        foreach (var y in new[] { 3.125f, 3.25f, 3.375f })
            At(-2.5f, y).Should().BeGreaterThan(cell * 0.2f, $"the space between two sheets two cells apart is open at {y}");
        UnloadModel(sheet);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Two_Double_Sided_Sheets_Crossing_Where_Four_Cells_Meet_Are_Both_Met_In_Those_Cells()
    {
        // Two upright sheets crossing along a line where four cells meet, as the voxel game's crops
        // cross, each of those cells lying as near both. Keeping one plane a cell, a point beside
        // the other sheet in such a cell read the cells' blend alone, half a cell, which a march
        // passes, and the world's probes standing there read dark round patches at the sheets' feet.
        var config = Config.Default.WithWindow("scene field crossing", 96, 64) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(1, 0.25f);
        var sheet = LoadModelFromMesh(GenMeshPlane(2, 2, 1, 1));
        sheet.Materials[0].DoubleSided = true;
        var camera = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 6; frame++)
        {
            BeginDrawing();
            BeginMode3D(camera);
            DrawModelEx(sheet, new Vector3(0, 1, 0), Vector3.UnitZ, 90, Vector3.One, Color.Gray);
            DrawModelEx(sheet, new Vector3(0, 1, 0), Vector3.UnitX, 90, Vector3.One, Color.Gray);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var fields = renderer.RenderWorld.TryGet<SceneFieldRenderer>()!;
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var (first, second) = (device.ReadSceneFieldSheets(fields.Field!), device.ReadSceneFieldSheets(fields.Field!, second: true));
        const int size = SceneFieldPlan.Resolution;
        const float cell = 0.25f;
        var origin = fields.Plan!.BuiltOrigin(0)!.Value;

        // A point's distance from the planes the cell it lies in keeps, as fieldDistanceSheetIn
        // takes them, the cells' blend left out since it holds no point there nearer than half a cell.
        float At(float x, float y, float z)
        {
            var at = new Vector3((int)MathF.Floor((x - origin.X) / cell), (int)MathF.Floor((y - origin.Y) / cell), (int)MathF.Floor((z - origin.Z) / cell));
            int index = ((int)at.Z * size + (int)at.Y) * size + (int)at.X;
            var local = new Vector3(x, y, z) - (origin + (at + new Vector3(0.5f)) * cell);
            float Plane(float[] planes)
            {
                var kept = new Vector3(planes[index * 3], planes[index * 3 + 1], planes[index * 3 + 2]);
                return kept.Length() < 0.5f ? float.MaxValue
                    : MathF.Abs(Vector3.Dot(local, Vector3.Normalize(kept)) - (kept.Length() - 2) * cell) - 0.25f * cell;
            }
            return MathF.Min(Plane(first), Plane(second));
        }

        // In each of the four cells about the crossing, beside each sheet.
        foreach (var (sx, sz) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            At(sx * cell * 0.02f, 1, sz * cell * 0.6f).Should().BeLessThan(cell * 0.1f, $"the sheet across x is met in the cell at {sx}, {sz}");
            At(sx * cell * 0.6f, 1, sz * cell * 0.02f).Should().BeLessThan(cell * 0.1f, $"the sheet across z is met in the cell at {sx}, {sz}");
        }
        UnloadModel(sheet);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Closed_Room_Of_Double_Sided_Sheets_Stays_Dark_Inside(GlobalIllumination quality)
    {
        // A room of double-sided sheets on a ground under a bright sky, each sheet lying midway
        // between two rows of cells, where a march between the cells' middles comes no nearer it
        // than half a cell, and its corners where two sheets meet, each cell there keeping the plane
        // of the nearer alone. The picture's mean light from inside, against the open ground's,
        // bound 15% over what it read on the development machine's GPU, 0.070 at Low and 0.036 at
        // High, the world's probes beyond a wall thinner than their spacing blended into the pixels
        // beside it. Its frames' sRGB mean read 0.054 and 0.013 of the open ground's, where with each
        // sheet held below zero within half a cell either side 0.104 and 0.016, and with the sheets'
        // planes held a tenth of a cell either side, the march stepping over them, and the probes
        // standing on them moved off by the field's gradient, which cancels across a sheet, 0.16 and
        // 0.037. Shifted off the cells' grid by 0.37 the room reads 0.004 at Low where 0.019.
        var config = Config.Default.WithWindow("sheet room", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(quality);
        var sky = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (sky[i * 4], sky[i * 4 + 1], sky[i * 4 + 2], sky[i * 4 + 3]) = i < 64 * 16 ? ((byte)120, (byte)165, (byte)230, (byte)255) : ((byte)90, (byte)100, (byte)110, (byte)255);
        SetEnvironmentMap(new Image(sky, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var sheet = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        sheet.Materials[0].DoubleSided = true;
        var stone = new Color(150, 150, 150);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var eye = new Vector3(0.5f, 1.4f, 0.8f);
        double Seen(bool closed, int frames)
        {
            var camera = new Camera3D(eye, eye + new Vector3(-0.6f, -0.3f, -1), Vector3.UnitY, 70);
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(64, 1, 64), stone);
                if (closed)
                {
                    DrawModelEx(sheet, new Vector3(-3, 1.5f, 0), Vector3.UnitZ, 90, new Vector3(3, 1, 6), stone);
                    DrawModelEx(sheet, new Vector3(3, 1.5f, 0), Vector3.UnitZ, 90, new Vector3(3, 1, 6), stone);
                    DrawModelEx(sheet, new Vector3(0, 1.5f, -3), Vector3.UnitX, 90, new Vector3(6, 1, 3), stone);
                    DrawModelEx(sheet, new Vector3(0, 1.5f, 3), Vector3.UnitX, 90, new Vector3(6, 1, 3), stone);
                    DrawModelEx(sheet, new Vector3(0, 3, 0), Vector3.UnitY, 0, new Vector3(6, 1, 6), stone);
                }
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int p = 0; p < 160 * 90 * 4; p += 4)
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            return sum / (160 * 90);
        }

        var open = Seen(closed: false, SceneFieldPlan.SettleFrames + 30);
        var inside = Seen(closed: true, SceneFieldPlan.SettleFrames + 60);
        (inside / open).Should().BeLessThan(quality == GlobalIllumination.Low ? 0.081 : 0.041,
            $"the sky's light does not pass the sheets, {inside / open:0.0000} of the open ground's at {quality}");
        UnloadModel(block);
        UnloadModel(sheet);
    }
}
