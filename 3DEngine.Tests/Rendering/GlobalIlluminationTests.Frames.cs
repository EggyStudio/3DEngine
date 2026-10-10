using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

public sealed partial class GlobalIlluminationTests
{
    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void The_Light_That_Bounced_Goes_Within_Two_Frames_Of_The_Light_That_Went_Out(GlobalIllumination quality)
    {
        // The room a glowing panel lights by bouncing alone, drawn into a render texture read each
        // frame after the panel goes dark. Each frame the probes' rays take the light that bounced
        // to what they meet the frame before, so it went on bouncing after the panel, its room
        // under a level 30 frames on at Low and 28 at High, and 45 at Low once surfaces weighed
        // their probes off themselves, as this test measured on an RTX 4070, where it goes the
        // frame after the own light of the probes around each falls, under a level 2 frames on,
        // the room's probes lit more by the panel's light bounced than by the panel.
        Open();
        SetGlobalIllumination(quality);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = Color.White;
        var target = LoadRenderTexture(160, 96);
        float Drawn(float glow, int frames)
        {
            panel.Materials[0].EmissiveIntensity = glow;
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
                DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
                DrawModelEx(slab, new Vector3(0, 1.5f, -1.85f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), Color.White);
                DrawModelEx(slab, new Vector3(-1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
                DrawModelEx(slab, new Vector3(1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
                DrawModelEx(panel, new Vector3(0, 2.97f, 0), Vector3.UnitY, 0, new Vector3(1.2f, 0.06f, 1.2f), Color.White);
                EndMode3D();
                EndTextureMode();
                ClearBackground(Color.Black);
                DrawTexture(target.Texture, 0, 0, Color.White);
                EndDrawing();
            }
            return Mean(LoadImageFromTexture(target.Texture), 0, 0, 160, 96).X;
        }

        var glowing = Drawn(8, SceneFieldPlan.SettleFrames + 30);
        var after = Enumerable.Range(0, 6).Select(_ => Drawn(0, 1)).ToList();
        glowing.Should().BeGreaterThan(100, "the panel lights its room by bouncing");
        after.FindIndex(level => level < 1).Should().BeLessThanOrEqualTo(2,
            $"the light that bounced goes with the panel, the room reading {string.Join(", ", after.Select(level => $"{level:0.0}"))} the frames after");

        // Taken whole, it lingers.
        GetApp().World.Resource<GlobalIlluminationSettings>().FollowOff = true;
        Drawn(8, 30);
        Drawn(0, 2).Should().BeGreaterThan(100, "where the probes take the whole of the light that bounced, it bounces on");
        UnloadRenderTexture(target);
        UnloadModel(slab);
        UnloadModel(panel);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Lamp_Put_Out_In_A_Room_Apart_Leaves_A_Corridor_Lit_By_Bounce_Alone_As_It_Was(GlobalIllumination quality)
    {
        // A lit room with a corridor out of it that turns a corner, the camera down the turned leg,
        // which only light that bounced around the corner reaches, and a second lit room apart,
        // whose lamp, bright enough to hold some half of every probe's own light, goes out at frame
        // 0. A probe lit mostly by light that bounced is judged by the probes around it, so the
        // corridor keeps its light to the tenth of a level; judged by every probe's own light, it
        // dimmed with the far room's lamp by 3.3 and 3.7% 3 frames on, as this test measured on an
        // RTX 4070.
        Open();
        SetGlobalIllumination(quality);
        CreatePointLight(new Vector3(0, 2.4f, 0), new Color(255, 236, 210), 14, range: 14, castsShadows: true);
        var apart = CreatePointLight(new Vector3(1, 2.4f, -8.5f), new Color(255, 236, 210), 80, range: 14, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var wall = new Color(220, 220, 215);
        var shots = new List<string>();
        for (int frame = -SceneFieldPlan.SettleFrames - 40; frame <= 6; frame++)
        {
            if (frame == 0) SetLightColor(apart, Color.Black);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(4.5f, 1.4f, 3.5f), new Vector3(4.5f, 1.0f, -3f), Vector3.UnitY, 60));
            // The room, 4 by 4, its wall at +x open from z -0.6 to 0.6, and the floor and ceiling over all.
            DrawModelEx(slab, new Vector3(-1, -0.15f, 0), Vector3.UnitY, 0, new Vector3(15, 0.3f, 9), wall);
            DrawModelEx(slab, new Vector3(-1, 3.15f, 0), Vector3.UnitY, 0, new Vector3(15, 0.3f, 9), wall);
            DrawModelEx(slab, new Vector3(-2.15f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4.6f), wall);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2.15f), Vector3.UnitY, 0, new Vector3(4.6f, 3, 0.3f), wall);
            DrawModelEx(slab, new Vector3(0, 1.5f, 2.15f), Vector3.UnitY, 0, new Vector3(4.6f, 3, 0.3f), wall);
            DrawModelEx(slab, new Vector3(2.15f, 1.5f, -1.4f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 1.6f), wall);
            DrawModelEx(slab, new Vector3(2.15f, 1.5f, 1.4f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 1.6f), wall);
            // The corridor, 1.2 wide, along x from the opening to x 5, then along +z to z 4.
            DrawModelEx(slab, new Vector3(3.6f, 1.5f, -0.75f), Vector3.UnitY, 0, new Vector3(2.9f, 3, 0.3f), wall);
            DrawModelEx(slab, new Vector3(5.45f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 1.8f), wall);
            DrawModelEx(slab, new Vector3(3.15f, 1.5f, 2.4f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 3.3f), wall);
            DrawModelEx(slab, new Vector3(5.45f, 1.5f, 2.6f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 3.4f), wall);
            DrawModelEx(slab, new Vector3(4.3f, 1.5f, 4.45f), Vector3.UnitY, 0, new Vector3(2.6f, 3, 0.3f), wall);
            // The room apart, closed, 4 by 3, behind the first room's back wall.
            DrawModelEx(slab, new Vector3(1, -0.15f, -8.5f), Vector3.UnitY, 0, new Vector3(4.6f, 0.3f, 3.6f), wall);
            DrawModelEx(slab, new Vector3(1, 3.15f, -8.5f), Vector3.UnitY, 0, new Vector3(4.6f, 0.3f, 3.6f), wall);
            DrawModelEx(slab, new Vector3(-1.15f, 1.5f, -8.5f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 3.6f), wall);
            DrawModelEx(slab, new Vector3(3.15f, 1.5f, -8.5f), Vector3.UnitY, 0, new Vector3(0.3f, 3, 3.6f), wall);
            DrawModelEx(slab, new Vector3(1, 1.5f, -10.15f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), wall);
            DrawModelEx(slab, new Vector3(1, 1.5f, -6.85f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), wall);
            EndMode3D();
            if (frame >= -1)
            {
                shots.Add(Path.Combine(_folder.Path, $"{_captures++}.png"));
                TakeScreenshot(shots[^1]);
            }
            EndDrawing();
        }
        UnloadModel(slab);
        CloseWindow();
        UseApp(null);

        var reads = shots.Select(path => Mean(LoadImage(path), 0, 0, 160, 96).X).ToList();
        reads[0].Should().BeGreaterThan(40, "the corridor is lit by the light that bounced around its corner");
        reads.Skip(1).Min().Should().BeGreaterThan(reads[0] * 0.985f,
            $"and keeps it as the far room goes dark, reading {string.Join(", ", reads.Select(level => $"{level:0.0}"))} from the frame before");
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Lamp_Carried_Away_Takes_The_Light_It_Bounced_With_It(GlobalIllumination quality)
    {
        // A room split by an inner wall, its lamp carried from one side to the other at frame 0,
        // each frame after against the frame 90 on, the light left where the lamp was, the picture
        // brighter than it settles, under half of it 4 frames on. Taken whole, as the probes took
        // the light that bounced before, it kept nine tenths of it 4 frames on, where each probe on
        // the lamp's old side taking it at the share its own light kept leaves 0.17 at Low and
        // 0.22 at High, as this test measured on an RTX 4070.
        Open();
        SetGlobalIllumination(quality);
        var lamp = CreatePointLight(new Vector3(-1.5f, 2.2f, -0.5f), new Color(255, 230, 200), 8, range: 10, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var shots = new List<string>();
        for (int frame = -SceneFieldPlan.SettleFrames - 40; frame <= 90; frame++)
        {
            if (frame == 0) SetLightPosition(lamp, new Vector3(1.6f, 2.2f, -0.5f));
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 1.7f, 4.2f), new Vector3(0, 1.1f, -1), Vector3.UnitY, 65));
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2.85f), Vector3.UnitY, 0, new Vector3(6, 3, 0.3f), Color.White);
            DrawModelEx(slab, new Vector3(-2.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(200, 60, 40));
            DrawModelEx(slab, new Vector3(2.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(60, 160, 200));
            DrawModelEx(slab, new Vector3(0, 1.1f, -1.4f), Vector3.UnitY, 0, new Vector3(0.3f, 2.2f, 2.6f), Color.White);
            EndMode3D();
            if (frame >= 0 && (frame <= 12 || frame == 90))
            {
                shots.Add(Path.Combine(_folder.Path, $"{_captures++}.png"));
                TakeScreenshot(shots[^1]);
            }
            EndDrawing();
        }
        UnloadModel(slab);
        CloseWindow();
        UseApp(null);

        var settled = LoadImage(shots[^1]);
        var left = shots.Take(13).Select(path =>
        {
            var image = LoadImage(path);
            double sum = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var (p, q) = (GetImageColor(image, x, y), GetImageColor(settled, x, y));
                    sum += Math.Max(p.R - q.R, 0) + Math.Max(p.G - q.G, 0) + Math.Max(p.B - q.B, 0);
                }
            return sum / (image.Width * image.Height * 3);
        }).ToList();
        left[0].Should().BeGreaterThan(1, "the lamp's light is left behind the frame it is carried");
        left[4].Should().BeLessThan(left[0] * 0.5,
            $"and goes, the light left reading {string.Join(", ", left.Select(level => $"{level:0.00}"))} frame by frame");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Light_That_Bounces_Follows_A_Lamp_Brought_Into_The_Room_Within_Two_Frames()
    {
        // The red wall and the block of the test above, the lamp held out of its reach and brought
        // in at frame 30, the camera between the two looking at the block's side facing the wall,
        // which only the wall's light reaches, so the screen's probes stand on it. The side is read
        // each frame from frame 28, and the frames after 30 it takes to come and stay within a
        // tenth of the way from its light before to its light at frame 59 are how far the bounce
        // lags the lamp, seven where each frame blended a fifth of its light.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        var lamp = CreatePointLight(new Vector3(0.5f, 60, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var reads = new List<(int Frame, string Path)>();
        for (int frame = 0; frame < 60; frame++)
        {
            if (frame == 30) SetLightPosition(lamp, new Vector3(0.5f, 2.6f, 0.8f));
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(-1.3f, 0.8f, 0.9f), new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 50));
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
            EndMode3D();
            if (frame >= 28)
            {
                reads.Add((frame, Path.Combine(_folder.Path, $"{_captures++}.png")));
                TakeScreenshot(reads[^1].Path);
            }
            EndDrawing();
        }
        UnloadModel(slab);
        CloseWindow();
        UseApp(null);

        var side = reads.Select(read => (read.Frame, Red: Mean(LoadImage(read.Path), 60, 30, 40, 36).X)).ToList();
        var (before, after) = (side[0].Red, side[^1].Red);
        (after - before).Should().BeGreaterThan(20, $"the lamp brought in lights the wall, whose light reaches the side, {before:0} to {after:0}");
        var lag = side.First(read => read.Frame >= 30 && side.Where(later => later.Frame >= read.Frame)
            .All(later => Math.Abs(later.Red - after) <= 0.1 * (after - before))).Frame - 30;
        lag.Should().BeLessThanOrEqualTo(2, $"the bounce follows the lamp, the side reading {string.Join(", ", side.Select(read => $"{read.Red:0}"))} from frame 28");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Light_That_Bounces_Holds_Still_As_The_Camera_Slides()
    {
        // A Cornell box the camera slides through a hundredth of a unit a frame, along x either way,
        // up, ahead and askew, the picture's change from frame to frame with light bouncing set
        // against its change with none. The screen's probes stand on whatever surface each tile's
        // middle shows, so as the camera slides they slide over the surfaces and their light
        // changes with them, which blending each with the frame before's where its surface was
        // holds still. One slide's change differs from the next's by as much as the bounce adds, so
        // the change is read over the five.
        var slide = Vector3.UnitX;
        double Change(GlobalIllumination quality, bool held = true)
        {
            Open();
            SetGlobalIllumination(quality);
            GetApp().World.Resource<GlobalIlluminationSettings>().HistoryOff = !held;
            CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            var shots = new List<string>();
            for (int frame = 0; frame < 50; frame++)
            {
                var moved = slide * (frame < 30 ? 0 : (frame - 30) * 0.01f);
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 2.5f, 8) + moved, new Vector3(0, 2.4f, 0) + moved, Vector3.UnitY, 45));
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawModelEx(slab, new Vector3(0, 5.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawModelEx(slab, new Vector3(0, 2.5f, -3.15f), Vector3.UnitY, 0, new Vector3(6, 5, 0.3f), Color.White);
                DrawModelEx(slab, new Vector3(-3.15f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5, 6), new Color(200, 30, 30));
                DrawModelEx(slab, new Vector3(3.15f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5, 6), new Color(30, 200, 30));
                DrawModelEx(slab, new Vector3(-1, 1, -0.5f), Vector3.UnitY, 20, new Vector3(1.5f, 2, 1.5f), Color.White);
                EndMode3D();
                if (frame >= 36) shots.Add(Path.Combine(_folder.Path, $"{_captures++}.png"));
                if (frame >= 36) TakeScreenshot(shots[^1]);
                EndDrawing();
            }
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
            var changes = new List<double>();
            for (int i = 1; i < shots.Count - 2; i++)
            {
                var (a, b) = (LoadImage(shots[i - 1]), LoadImage(shots[i]));
                double sum = 0;
                int n = 0;
                for (int y = 8; y < 88; y++)
                    for (int x = 16; x < 144; x++, n += 3)
                    {
                        var (p, q) = (GetImageColor(a, x, y), GetImageColor(b, x, y));
                        sum += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
                    }
                changes.Add(sum / n);
            }
            return changes.Average();
        }

        // What the bounce adds to the change, held by the frame before's light and not, as a share,
        // since a bounce twice as bright moves twice as many levels: 0.86 levels a frame of 3.10 on
        // an RTX 4070, 0.51 to 1.28 and 1.39 to 3.91 by slide, as this test measures them, where a
        // history that held nothing would leave the whole.
        var (adds, unhelds) = (new List<double>(), new List<double>());
        foreach (var way in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, new Vector3(0.7f, 0.3f, -0.6f) })
        {
            slide = way;
            var still = Change(GlobalIllumination.Off);
            adds.Add(Change(GlobalIllumination.Low) - still);
            unhelds.Add(Change(GlobalIllumination.Low, held: false) - still);
        }
        adds.Average().Should().BeLessThan(unhelds.Average() * 0.6,
            $"the frame before's light takes most of the bounce's crawl away, adding {adds.Average():0.00} levels a frame ({adds.Min():0.00} to {adds.Max():0.00}) where unheld it adds {unhelds.Average():0.00} ({unhelds.Min():0.00} to {unhelds.Max():0.00})");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Mesh_Replaced_Beside_A_Lamp_Keeps_The_Light_That_Bounces_While_Its_Replacement_Settles()
    {
        // Three walls around a lamp drawn apart, which lights them by bouncing alone, the walls one
        // mesh replaced at frame 60 by one of four, the fourth a block beside the lamp, as a game's
        // section is meshed again when a block is placed. The walls left the field at once and their
        // replacement stood in as a few boxes until it settled, the wall reading 116 before and 69
        // six frames after and the floor 85 and 60, as this test measured on an RTX 4070; kept in
        // the field until the replacement settles, neither fell.
        Open();
        SetGlobalIllumination(GlobalIllumination.Medium);
        SetAmbientLight(Color.Black, 0);
        var cube = GenMeshCube(1, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(cube.Id, out var unit, out var unitIndices).Should().BeTrue();
        ModelMesh Boxes(params (Vector3 At, Vector3 Size)[] boxes) => UploadMesh(
            [.. boxes.SelectMany(b => unit.Select(v => v with { Position = v.Position * b.Size + b.At }))],
            [.. boxes.SelectMany((_, n) => unitIndices.Select(i => i + (uint)(n * unit.Length)))]);
        (Vector3, Vector3)[] walls = [(new Vector3(0, 1.5f, -1.5f), new Vector3(4, 3, 0.5f)), (new Vector3(-2, 1.5f, 0), new Vector3(0.5f, 3, 3)),
            (new Vector3(2, 1.5f, 0), new Vector3(0.5f, 3, 3))];
        var (before, after) = (Boxes(walls), Boxes([.. walls, (new Vector3(-0.9f, 0.5f, -0.6f), Vector3.One)]));
        var floor = GenMeshPlane(8, 8, 1, 1);
        var lamp = GenMeshCube(0.4f, 0.4f, 0.4f);
        var (white, glow) = (new ModelMaterial(Color.White), new ModelMaterial(Color.White) { Emissive = Color.White, EmissiveIntensity = 8 });
        var camera = new Camera3D(new Vector3(0, 2.5f, 4), new Vector3(0, 0.5f, 0), Vector3.UnitY, 50);
        var target = LoadRenderTexture(160, 96);
        var (wallAt, floorAt) = (GetWorldToScreen(new Vector3(0.6f, 1.5f, -1.24f), camera), GetWorldToScreen(new Vector3(0.8f, 0, 0.6f), camera));
        (float Wall, float Floor) Drawn(ModelMesh walls)
        {
            BeginDrawing();
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(floor, white, Matrix4x4.Identity);
            DrawMesh(walls, white, Matrix4x4.Identity);
            DrawMesh(lamp, glow, Matrix4x4.CreateTranslation(0, 1, -0.6f));
            EndMode3D();
            EndTextureMode();
            ClearBackground(Color.Black);
            DrawTexture(target.Texture, 0, 0, Color.White);
            EndDrawing();
            var image = LoadImageFromTexture(target.Texture);
            return (Mean(image, (int)wallAt.X - 2, (int)wallAt.Y - 2, 5, 5).X, Mean(image, (int)floorAt.X - 2, (int)floorAt.Y - 2, 5, 5).X);
        }

        for (int frame = 0; frame < 59; frame++) Drawn(before);
        var lit = Drawn(before);
        lit.Wall.Should().BeGreaterThan(60, "the lamp lights the wall by bouncing");
        var edited = Enumerable.Range(0, SceneFieldPlan.SettleFrames + 4).Select(_ => Drawn(after)).ToList();
        foreach (var (wall, ground) in edited)
        {
            wall.Should().BeGreaterThan(lit.Wall * 0.95f, $"the wall keeps its light while the walls' replacement settles, {string.Join(", ", edited.Select(e => $"{e.Wall:0}"))} after {lit.Wall:0}");
            ground.Should().BeGreaterThan(lit.Floor * 0.95f, $"and the floor its, {string.Join(", ", edited.Select(e => $"{e.Floor:0}"))} after {lit.Floor:0}");
        }
        UnloadRenderTexture(target);
        UnloadMesh(before);
        UnloadMesh(after);
        UnloadMesh(floor);
        UnloadMesh(lamp);
        UnloadMesh(cube);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Room_Sealed_From_The_Sky_Reflects_None_Of_It_Where_An_Open_Floor_Reflects_It()
    {
        // A box sealed on every side and a floor under the open sky beside it, both of stone as
        // rough as the voxel game's, under a blue sky, looked at along the box's inside wall and
        // across the floor at a grazing angle, where a rough surface reflects most. Weighed by the
        // occlusion alone the sky's reflection read 37.5 in blue inside the box, the open floor
        // 62.7; weighed by the share of the sky the probes see, 0.13 and 61.8, as this test
        // measured on an RTX 4070.
        Open();
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var slab = GenMeshCube(1, 1, 1);
        var stone = new ModelMaterial(Color.Gray) { Roughness = 0.9f };
        var target = LoadRenderTexture(160, 96);
        var inside = new Camera3D(new Vector3(-1.6f, 1.2f, 1.6f), new Vector3(1.8f, 1.1f, -1.2f), Vector3.UnitY, 60);
        var open = new Camera3D(new Vector3(8, 1.2f, 3), new Vector3(8, 0, -3), Vector3.UnitY, 60);
        float Read(Camera3D camera, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                BeginDrawing();
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                // The box, four by three by four inside, and the floor beside it.
                foreach (var (at, size) in new[] { (new Vector3(0, -0.25f, 0), new Vector3(5, 0.5f, 5)), (new Vector3(0, 3.25f, 0), new Vector3(5, 0.5f, 5)),
                    (new Vector3(0, 1.5f, -2.25f), new Vector3(5, 3, 0.5f)), (new Vector3(0, 1.5f, 2.25f), new Vector3(5, 3, 0.5f)),
                    (new Vector3(-2.25f, 1.5f, 0), new Vector3(0.5f, 3, 4)), (new Vector3(2.25f, 1.5f, 0), new Vector3(0.5f, 3, 4)),
                    (new Vector3(8, -0.25f, 0), new Vector3(6, 0.5f, 8)) })
                    DrawMesh(slab, stone, Matrix4x4.CreateScale(size) * Matrix4x4.CreateTranslation(at));
                EndMode3D();
                EndTextureMode();
                ClearBackground(Color.Black);
                DrawTexture(target.Texture, 0, 0, Color.White);
                EndDrawing();
            }
            return Mean(LoadImageFromTexture(target.Texture), 0, 0, 160, 96).Z;
        }

        var dark = (Inside: Read(inside, SceneFieldPlan.SettleFrames + 30), Open: Read(open, 20));
        SetEnvironmentMap(GenImageColor(64, 32, new Color(60, 120, 255)));
        var sky = (Inside: Read(inside, 30), Open: Read(open, 30));
        sky.Open.Should().BeGreaterThan(dark.Open + 40, "the open floor reflects the sky");
        sky.Inside.Should().BeLessThan(dark.Inside + 3, $"and the sealed room none of it, {sky.Inside:0.0} in blue against {dark.Inside:0.0} with no sky");
        UnloadRenderTexture(target);
        UnloadMesh(slab);
    }
}
