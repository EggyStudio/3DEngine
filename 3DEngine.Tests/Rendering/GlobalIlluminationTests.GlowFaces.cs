using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The faces a glow light keeps once the field shows what stands around it (glow_faces.slang), with
// lamps of blocks set in rooms of blocks meshed both ways, every block's face drawn and the faces
// toward a lamp left out as a program that culls them does, read from the lights after the frames,
// and the light they send held against the path-traced reference at one bounce.
public sealed partial class GlobalIlluminationTests
{
    // How a lamp block is handed to the engine, as a unit cube, as one mesh of the block's faces no
    // block of the room lies against, or as each of its six faces a mesh of its own.
    public enum Handed { Cube, Shown, Quads }

    // The ways out of a block, in the order glowFaces numbers its faces: -x, +x, -y, +y, -z, +z.
    private static readonly (int X, int Y, int Z)[] Ways = [(-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1)];

    // The room's blocks: a shell around the air from -3 to 2 along x and z and 0 to 2 up, a ledge a
    // block high along the east wall, the east wall and the roof two blocks thick and the rest one.
    private static HashSet<(int X, int Y, int Z)> RoomBlocks()
    {
        var blocks = new HashSet<(int X, int Y, int Z)>();
        for (int x = -4; x <= 4; x++)
            for (int y = -1; y <= 4; y++)
                for (int z = -4; z <= 3; z++)
                    if (x < -3 || x > 2 || z < -3 || z > 2 || y < 0 || y > 2 || x == 2 && y == 0)
                        blocks.Add((x, y, z));
        return blocks;
    }

    // Where each room sets its lamp blocks, the room's blocks they stand in place of, and the faces
    // each lamp place's light keeps, a bit a face as glowFaces numbers them.
    private static ((int X, int Y, int Z)[] Lamps, int[] Faces) Placed(string room) => room switch
    {
        "flush in the east wall" => ([(3, 1, 0)], [1 << 0]),
        "in the west wall a block thick" => ([(-4, 1, 0)], [1 << 0 | 1 << 1]),
        "in the ledge's inner corner" => ([(2, 0, 0)], [1 << 0 | 1 << 3]),
        "on the floor" => ([(0, 0, 0)], [63 & ~(1 << 2)]),
        "hung in the air" => ([(0, 1, 0)], [63]),
        "three joined in the ceiling" => ([(-1, 3, 0), (0, 3, 0), (1, 3, 0)], [1 << 2]),
        _ => throw new ArgumentException(room),
    };

    // A lamp's meshes as it is handed, each with where it is drawn in the room, of the light given.
    private static List<(ModelMesh Mesh, Matrix4x4 At, Color Light)> LampMeshes((int X, int Y, int Z) lamp, Handed handed, HashSet<(int X, int Y, int Z)> blocks, Color light)
    {
        HashSet<(int X, int Y, int Z)> own = [lamp];
        (int X, int Y, int Z) Next((int X, int Y, int Z) way) => (lamp.X + way.X, lamp.Y + way.Y, lamp.Z + way.Z);
        return handed switch
        {
            Handed.Cube => [(GenMeshCube(1, 1, 1), Matrix4x4.CreateTranslation(lamp.X + 0.5f, lamp.Y + 0.5f, lamp.Z + 0.5f), light)],
            Handed.Shown => [(Section(own, [.. blocks]), Matrix4x4.Identity, light)],
            _ => [.. Ways.Select(way => (Section(own, [.. Ways.Where(other => other != way).Select(Next)]), Matrix4x4.Identity, light))],
        };
    }

    // Opens a frame of a room, its field of cells a quarter of a block wide about the point given,
    // global illumination at the quality given.
    private static void OpenRoom(Vector3 center, GlobalIllumination quality = GlobalIllumination.Low)
    {
        Open();
        SetSceneField(4, 0.25f, 2);
        SetSceneFieldCenter(center);
        SetGlobalIllumination(quality);
    }

    // Draws a room's walls, a part of them drawn apart where given, and its lamps for a number of
    // frames, shifted by a part of a cell, the camera inside, and tells each frame's end.
    private static void DrawRoom(ModelMesh walls, IReadOnlyList<(ModelMesh Mesh, Matrix4x4 At, Color Light)> lamps, float shift, int frames, Action? after = null, ModelMesh? apart = null)
    {
        var stone = new ModelMaterial(new Color(180, 180, 180)) { Roughness = 1 };
        var glows = lamps.Select(lamp => new ModelMaterial(lamp.Light) { Emissive = lamp.Light, EmissiveIntensity = 4, Roughness = 1 }).ToArray();
        var at = Matrix4x4.CreateTranslation(shift, 0, shift);
        var camera = new Camera3D(new Vector3(-2.5f + shift, 2, -2.5f + shift), new Vector3(1 + shift, 0.8f, 1 + shift), Vector3.UnitY, 70);
        for (int frame = 0; frame < frames; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(walls, stone, at);
            if (apart is { } part) DrawMesh(part, stone, at);
            for (int i = 0; i < lamps.Count; i++) DrawMesh(lamps[i].Mesh, glows[i], lamps[i].At * at);
            EndMode3D();
            EndDrawing();
            after?.Invoke();
        }
    }

    private static (GraphicsDevice Device, GpuIllumination Gi) LitBy()
    {
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        return ((GraphicsDevice)renderer.Context.Graphics!, renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!);
    }

    // What the lights and the faces kept hold after the frame drawn last, asked for now and read once
    // the frames that follow have finished with them.
    private static (ShaderReadbackTicket Lights, ShaderReadbackTicket Kept) AskPlaces()
    {
        var (device, gi) = LitBy();
        return (device.ReadBack(gi.Lights, GlowLights.Offset, GlowLights.Head + GlowLights.Most * GlowLights.Bytes),
            device.ReadBack(gi.GlowFaces!, 0, GlowLights.Most * 4));
    }

    // Each glow light place as glow_faces.slang left it: its middle, the faces it emits from, none
    // where its reach is 0, its share, and the state the pass keeps of it, bits 0 to 5 its faces, bit
    // 6 set once decided and bits 7 to 10 the cascade it was decided in.
    private static (Vector3 Middle, int Faces, float Share, uint State)[] ReadPlaces((ShaderReadbackTicket Lights, ShaderReadbackTicket Kept) asked)
    {
        var (device, _) = LitBy();
        for (int frame = 0; frame < 8 && !(device.IsReadbackReady(asked.Lights) && device.IsReadbackReady(asked.Kept)); frame++)
        {
            BeginDrawing();
            EndDrawing();
        }
        var bytes = new byte[GlowLights.Head + GlowLights.Most * GlowLights.Bytes];
        device.ReadReadback(asked.Lights, bytes);
        var kept = new byte[GlowLights.Most * 4];
        device.ReadReadback(asked.Kept, kept);
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        var states = MemoryMarshal.Cast<byte, uint>(kept.AsSpan());
        var places = new (Vector3, int, float, uint)[Math.Clamp((int)floats[0], 0, GlowLights.Most)];
        for (int place = 0; place < places.Length; place++)
        {
            var at = (GlowLights.Head + place * GlowLights.Bytes) / 4;
            var w = floats[at + 19];
            var faces = (int)MathF.Floor(w * 0.5f);
            places[place] = (new Vector3(floats[at], floats[at + 1], floats[at + 2]), floats[at + 3] <= 0 ? 0 : faces == 0 ? 63 : faces,
                w - 2 * MathF.Floor(w * 0.5f), states[place]);
        }
        return places;
    }

    // Draws a room for the frames given and reads its places as the last of them left them, the room
    // drawn on while the reads finish, so no frame without it comes between.
    private static (Vector3 Middle, int Faces, float Share, uint State)[] DrawAndRead(ModelMesh walls,
        IReadOnlyList<(ModelMesh Mesh, Matrix4x4 At, Color Light)> lamps, float shift, int frames)
    {
        (ShaderReadbackTicket, ShaderReadbackTicket)? asked = null;
        var drawn = 0;
        DrawRoom(walls, lamps, shift, frames + 8, () =>
        {
            if (++drawn == frames) asked = AskPlaces();
        });
        return ReadPlaces(asked!.Value);
    }

    // Places in the order of their middles, by which two reads of the same lamps are held together.
    private static (Vector3 Middle, int Faces, float Share, uint State)[] ByMiddle((Vector3 Middle, int Faces, float Share, uint State)[] places) =>
        [.. places.OrderBy(place => place.Middle.X).ThenBy(place => place.Middle.Y).ThenBy(place => place.Middle.Z)];

    // A room of blocks with lamps set in it, meshed with the faces toward the lamps drawn or left
    // out, handed as asked, shifted by a part of a cell, after the field and its lights settle; each
    // lamp place as the lights hold it.
    private static (Vector3 Middle, int Faces, float Share, uint State)[] RoomPlaces(string room, bool culled, float shift, Handed handed = Handed.Cube, Vector3? center = null)
    {
        OpenRoom(center ?? new Vector3(shift, 1.5f, shift));
        var (lamps, _) = Placed(room);
        var blocks = RoomBlocks();
        foreach (var lamp in lamps) blocks.Remove(lamp);
        var walls = Section(blocks, culled ? lamps : []);
        var meshes = lamps.SelectMany(lamp => LampMeshes(lamp, handed, blocks, Color.White)).ToList();
        var places = DrawAndRead(walls, meshes, shift, SceneFieldPlan.SettleFrames + 12);
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
        return places;
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData("flush in the east wall", false, 0f)]
    [InlineData("flush in the east wall", true, 0f)]
    [InlineData("flush in the east wall", true, 0.1f)]
    [InlineData("in the west wall a block thick", false, 0f)]
    [InlineData("in the ledge's inner corner", false, 0f)]
    [InlineData("in the ledge's inner corner", true, 0f)]
    [InlineData("on the floor", false, 0f)]
    [InlineData("hung in the air", false, 0f)]
    [InlineData("three joined in the ceiling", false, 0f)]
    public void A_Lamps_Faces_Against_The_Blocks_Around_It_Are_Closed_And_The_Rest_Kept(string room, bool culled, float shift)
    {
        // The lamps a whole block of six faces each, so the faces the blocks around them cover are
        // the field's to close. A lamp keeps the face toward the room alone flush in a wall two
        // blocks thick, that and the back toward the air outside in a wall one block thick, two in
        // a corner, five on the floor and six in the air, and three lamps in a row in the roof are
        // one light whose bottom alone shows.
        var places = RoomPlaces(room, culled, shift);
        places.Select(place => place.Faces).Should().Equal(Placed(room).Faces,
            $"a lamp {room}, the faces toward it {(culled ? "left out" : "drawn")}, keeps the faces that show");
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(Handed.Cube)]
    [InlineData(Handed.Shown)]
    [InlineData(Handed.Quads)]
    public void A_Lamp_Flush_In_A_Wall_Sends_From_The_Face_Toward_The_Room_Whole_However_It_Is_Handed(Handed handed)
    {
        // The lamp flush in the east wall as a cube, as the one face of it that shows, and as six
        // faces apart. Each sends from its face toward the room at its whole surface and from none of
        // the four the wall's blocks lie against. Its back lies against the wall's second block where
        // the lamp is a block, and over the lamp's own space, which nothing of the room sees, where
        // the face that shows is all of it.
        var place = RoomPlaces("flush in the east wall", false, 0, handed).Should().ContainSingle().Subject;
        (place.Faces & 1 << 0).Should().NotBe(0, $"the lamp handed as {handed} sends toward the room");
        (place.Faces & (1 << 2 | 1 << 3 | 1 << 4 | 1 << 5)).Should().Be(0, $"the lamp handed as {handed} sends into none of the blocks beside it");
        if (handed != Handed.Shown)
            (place.Faces & 1 << 1).Should().Be(0, $"the lamp handed as {handed} sends nothing into the block behind it");
        place.Share.Should().BeApproximately(1, 1e-3f, $"the lamp handed as {handed} sends from its faces whole");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Two_Lamps_Of_Different_Light_Side_By_Side_Close_The_Faces_They_Press_Together()
    {
        // A white lamp and a red one side by side on the floor, away from the ledge, two lights since
        // their light differs, each closing the face the other lies against and the one on the floor.
        OpenRoom(new Vector3(0, 1.5f, 0));
        var blocks = RoomBlocks();
        var walls = Section(blocks, []);
        var meshes = LampMeshes((-1, 0, 0), Handed.Cube, blocks, Color.White).Concat(LampMeshes((0, 0, 0), Handed.Cube, blocks, Color.Red)).ToList();
        var places = DrawAndRead(walls, meshes, 0, SceneFieldPlan.SettleFrames + 12);
        places.Should().HaveCount(2, "lamps of different light are lights apart");
        places.Single(p => p.Middle.X < 0).Faces.Should().Be(63 & ~(1 << 2) & ~(1 << 1), "the white lamp sends neither into the floor nor into the red one");
        places.Single(p => p.Middle.X > 0).Faces.Should().Be(63 & ~(1 << 2) & ~(1 << 0), "the red lamp sends neither into the floor nor into the white one");
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
    }

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(2, 63)]
    [InlineData(1, 63 & ~(1 << 1) & ~(1 << 3))]
    public void A_Lamp_Hung_Near_The_Wall_And_The_Roof_Keeps_The_Faces_That_Look_Across_The_Gap(int cells, int faces)
    {
        // A lamp hung in the air with the east wall and the roof a number of cells off its east face
        // and its top. Two cells off, the faces see the gap and stay. One cell off, the march's second
        // cell lies in the wall, which it reads as the face lying against it, and the two faces close,
        // the error the cells' size gives a lamp that near.
        OpenRoom(new Vector3(0, 1.5f, 0));
        var blocks = RoomBlocks();
        var walls = Section(blocks, []);
        var gap = cells * 0.25f;
        var cube = GenMeshCube(1, 1, 1);
        List<(ModelMesh, Matrix4x4, Color)> lamp = [(cube, Matrix4x4.CreateTranslation(2.5f - gap, 2.5f - gap, 0.5f), Color.White)];
        DrawAndRead(walls, lamp, 0, SceneFieldPlan.SettleFrames + 12).Should().ContainSingle()
            .Which.Faces.Should().Be(faces, $"a lamp {cells} cells from the wall and the roof");
        UnloadMesh(walls);
        UnloadMesh(cube);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Half_Sunk_In_The_Floor_Keeps_Its_Sides_Whole()
    {
        // A lamp sunk half its height into the floor, whose sides have two points of four in the
        // floor and two above it, which keeps them. A side sends from its whole face, the half in the
        // floor too, the known error of a lamp so placed, and the bottom, all in the floor, closes.
        OpenRoom(new Vector3(0, 1.5f, 0));
        var blocks = RoomBlocks();
        var walls = Section(blocks, []);
        var cube = GenMeshCube(1, 1, 1);
        DrawAndRead(walls, [(cube, Matrix4x4.CreateTranslation(-1.5f, 0, -1.5f), Color.White)], 0, SceneFieldPlan.SettleFrames + 12).Should().ContainSingle()
            .Which.Faces.Should().Be(63 & ~(1 << 2), "the half sunk lamp keeps its sides and its top");
        UnloadMesh(walls);
        UnloadMesh(cube);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Decided_In_The_Second_Cascade_Is_Decided_Again_Alike_When_The_First_Holds_It()
    {
        // The lamp flush in the east wall with the field's middle far enough west that only the
        // second cascade, of cells half a block wide, holds it, and then with the field about the
        // room, where the first holds it and its faces are read again in the finer cells.
        OpenRoom(new Vector3(-7, 1.5f, 0));
        var (lamps, expected) = Placed("flush in the east wall");
        var blocks = RoomBlocks();
        foreach (var lamp in lamps) blocks.Remove(lamp);
        var walls = Section(blocks, []);
        var meshes = LampMeshes(lamps[0], Handed.Cube, blocks, Color.White);
        var coarse = DrawAndRead(walls, meshes, 0, SceneFieldPlan.SettleFrames + 12).Should().ContainSingle().Subject;
        ((coarse.State >> 7) & 15).Should().Be(1, "the second cascade decided the faces");
        coarse.Faces.Should().Be(expected[0], "the second cascade's cells close the faces the wall covers");

        SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
        var fine = DrawAndRead(walls, meshes, 0, SceneFieldPlan.SettleFrames + 12).Should().ContainSingle().Subject;
        ((fine.State >> 7) & 15).Should().Be(0, "the faces are decided again once the first cascade holds the lamp");
        fine.Faces.Should().Be(expected[0], "the first cascade's cells read the faces the second did");
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Still_Rooms_Lamps_Keep_Their_Faces_Frame_After_Frame_And_Through_A_Shift_Of_The_Field()
    {
        // The three lamps joined in the roof and the lamp in the ledge's corner, read each of 30
        // frames once the room settles, and again once the field's middle moved by its cascades'
        // grid, which shifts the cascades and builds their new cells far from the lamps.
        OpenRoom(new Vector3(0, 1.5f, 0));
        var blocks = RoomBlocks();
        (int X, int Y, int Z)[] lamps = [(-1, 3, 0), (0, 3, 0), (1, 3, 0), (2, 0, 0)];
        foreach (var lamp in lamps) blocks.Remove(lamp);
        var walls = Section(blocks, []);
        var meshes = lamps.SelectMany(lamp => LampMeshes(lamp, Handed.Cube, blocks, Color.White)).ToList();
        var asked = new List<(ShaderReadbackTicket, ShaderReadbackTicket)>();
        var drawn = 0;
        DrawRoom(walls, meshes, 0, SceneFieldPlan.SettleFrames + 12 + 30 + 8, () =>
        {
            if (++drawn > SceneFieldPlan.SettleFrames + 12 && asked.Count < 30) asked.Add(AskPlaces());
        });
        var frames = asked.Select(ticket => ByMiddle(ReadPlaces(ticket))).ToList();
        var first = frames[0];
        first.Select(place => place.Faces).Should().Equal([1 << 2, 1 << 0 | 1 << 3], "the joined lamps send down and the corner's lamp toward the room and up");
        foreach (var frame in frames)
            frame.Select(place => (place.Faces, place.State)).Should().Equal(first.Select(place => (place.Faces, place.State)),
                "a still room's lamps keep their faces and the state they were decided in");

        SetSceneFieldCenter(new Vector3(2, 1.5f, 0));
        ByMiddle(DrawAndRead(walls, meshes, 0, SceneFieldPlan.SettleFrames + 12)).Select(place => (place.Faces, place.State))
            .Should().Equal(first.Select(place => (place.Faces, place.State)), "the field's shift leaves the lamps' faces as they were");
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
    }

    [NeedsRayQueryTheory]
    [Trait("Category", "Render")]
    [InlineData("flush in the east wall")]
    [InlineData("on the floor")]
    [InlineData("hung in the air")]
    public void A_Probes_Hits_Take_A_Lamps_Light_As_The_Path_Traced_Reference_Does_At_One_Bounce(string room)
    {
        // The probe in the room's middle at High, its own rays' hits against a reference whose paths
        // bounce once past the probe's hit, a lamp's light at a hit being an emitter's met by that
        // bounce, the lamp flush in a wall sending from one face, on the floor from five and hung
        // from six.
        OpenRoom(new Vector3(0, 1.5f, 0), GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        GetApp().World.Resource<GlobalIlluminationSettings>().AgainOff = true;
        var (lamps, _) = Placed(room);
        var blocks = RoomBlocks();
        foreach (var lamp in lamps) blocks.Remove(lamp);
        var walls = Section(blocks, []);
        var meshes = lamps.SelectMany(lamp => LampMeshes(lamp, Handed.Cube, blocks, Color.White)).ToList();
        DrawRoom(walls, meshes, 0, SceneFieldPlan.SettleFrames + 20);
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(-1.5f, 1.5f, -1.5f), 256, 1, 0, out var why);
        probe.Should().NotBeNull(why);
        probe!.HitReference.X.Should().BeGreaterThan(0, "the lamp lights what the probe's rays meet");
        (probe.HitRays.X / probe.HitReference.X).Should().BeInRange(0.9f, 1.1f,
            $"a lamp {room} lights the probe's hits as the reference does, its hits bringing {probe.HitRays} against {probe.HitReference}");
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void The_Screen_Probes_On_A_Lamps_Wall_Above_A_Ledge_Bring_The_Light_Along_Their_Rays_As_The_Reference_Does()
    {
        // The screen's probes on the east wall above the ledge, a lamp set flush in the wall, their
        // rays against the reference along the same ways, the light past the first bounce left out.
        // A ray that goes into the ledge through its top, which the eye sees edge on, meets the top
        // by the lamp and not the floor past the ledge in its shadow. The window is 480 by 270, so the
        // wall holds the screen's probes in rows and the depth holds the ledge's top a few texels
        // high.
        var config = Config.Default.WithWindow("gi test", 480, 270) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetSceneFieldCenter(new Vector3(0, 1.5f, 0));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        settings.AgainOff = true;
        var (lamps, _) = Placed("flush in the east wall");
        var blocks = RoomBlocks();
        foreach (var lamp in lamps) blocks.Remove(lamp);
        var walls = Section(blocks, []);
        var meshes = lamps.SelectMany(lamp => LampMeshes(lamp, Handed.Cube, blocks, Color.White)).ToList();
        DrawRoom(walls, meshes, 0, SceneFieldPlan.SettleFrames + 20);
        var screen = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!;
        var camera = new Camera3D(new Vector3(-2.5f, 2, -2.5f), new Vector3(1, 0.8f, 1), Vector3.UnitY, 70);
        var (down, downReference, up, upReference, probes) = (0f, 0f, 0f, 0f, 0);
        foreach (var y in new[] { 1.3f, 1.7f, 2.3f, 2.7f })
            foreach (var z in new[] { -2.5f, -1.5f, -0.5f, 1.5f, 2.5f })
            {
                var at = GetWorldToScreen(new Vector3(3, y, z), camera);
                if (at.X < 0 || at.Y < 0 || at.X >= GetScreenWidth() || at.Y >= GetScreenHeight())
                    continue;
                settings.LoggedScreenProbe = (int)at.Y / screen.Tile * screen.Across + (int)at.X / screen.Tile;
                DrawRoom(walls, meshes, 0, 2);
                var probe = BounceReference.MeasureScreenProbe(GetApp().World, screen, 256, 1, out var why);
                probe.Should().NotBeNull(why);
                if (probe!.Normal.X > -0.9f)
                    continue;
                probes++;
                foreach (var ray in probe.Rays)
                    if (ray.Way.Y < 0)
                        (down, downReference) = (down + ray.Light.X * ray.Share, downReference + ray.Reference.X * ray.Share);
                    else
                        (up, upReference) = (up + ray.Light.X * ray.Share, upReference + ray.Reference.X * ray.Share);
            }
        probes.Should().BeGreaterThanOrEqualTo(6, "the wall above the ledge holds as many of the screen's probes");
        output.WriteLine($"the {probes} probes on the east wall bring {down / downReference:0.000} of the reference's light along their rays going down and {up / upReference:0.000} along those going up");
        (down / downReference).Should().BeInRange(0.85f, 1f, $"the {probes} probes on the east wall bring {down:0.0000} along their rays toward the ledge and the floor where the reference brings {downReference:0.0000}");
        UnloadMesh(walls);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);
    }

    [NeedsRayQueryTheory]
    [Trait("Category", "Render")]
    [InlineData("flush in the east wall")]
    [InlineData("on the floor")]
    [InlineData("hung in the air")]
    public void The_Light_A_Lamp_Bounces_Onto_The_East_Wall_Is_The_References_Whether_The_Lamp_Is_Set_In_It_Or_Not(string room)
    {
        // The probes' light on the east wall against the reference's light bounced once, its paths
        // at two bounces less its paths at one, the light past the first bounce left out. A lamp set
        // flush in the wall sends it nothing straight, its face lying in the wall's plane, so all the
        // wall takes comes back from the room, from hits that see the lamp's face at a slant.
        OpenRoom(new Vector3(0, 1.5f, 0), GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        settings.AgainOff = true;
        var (lamps, _) = Placed(room);
        var blocks = RoomBlocks();
        foreach (var lamp in lamps) blocks.Remove(lamp);
        // The ledge a mesh of its own beside the walls, so the reference names its front apart from
        // the east wall above it.
        var ledge = blocks.Where(block => block.X == 2 && block.Y == 0).ToHashSet();
        var rest = blocks.Except(ledge).ToHashSet();
        var (walls, front) = (Section(rest, [.. ledge]), Section(ledge, [.. rest]));
        var meshes = lamps.SelectMany(lamp => LampMeshes(lamp, Handed.Cube, blocks, Color.White)).ToList();
        DrawRoom(walls, meshes, 0, SceneFieldPlan.SettleFrames + 20, apart: front);
        var (once, twice) = (Path.Combine(_folder.Path, "once.png"), Path.Combine(_folder.Path, "twice.png"));
        BounceReference.Trace(GetApp().World, once, 256, 1).Should().StartWith("traced");
        BounceReference.Trace(GetApp().World, twice, 256, 2).Should().StartWith("traced");
        settings.Term = 1;
        DrawRoom(walls, meshes, 0, 3, apart: front);
        var wall = Bounced("6");
        var ledgeFront = Bounced("1");
        output.WriteLine($"a lamp {room}: the probes bring the east wall above the ledge {wall:0.000} of the reference's light bounced once and the ledge's front {ledgeFront:0.000}");
        wall.Should().BeInRange(0.9f, 1.15f, $"the probes bring the east wall above the ledge {wall:0.000} of the reference's light bounced once with a lamp {room}");
        ledgeFront.Should().BeInRange(0.95f, 1.18f, $"the probes bring the ledge's front {ledgeFront:0.000} of the reference's light bounced once with a lamp {room}");
        settings.Term = 0;
        UnloadMesh(walls);
        UnloadMesh(front);
        foreach (var (mesh, _, _) in meshes) UnloadMesh(mesh);

        // The probes' light on the faces toward -x of the mesh as high as given, the east wall's six
        // or the ledge's one, over the reference's light bounced once there, its paths at two
        // bounces less its paths at one.
        float Bounced(string high)
        {
            var (straight, probes) = Faces(once, high);
            var (both, _) = Faces(twice, high);
            return probes / (both - straight);
        }

        // The mean light of those faces in the reference at the path given and in the frame.
        static (float Reference, float Frame) Faces(string path, string high)
        {
            var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
            regions.Should().NotBeNull(why);
            var faces = regions!.Single(region => region.Name.EndsWith($"({high} high), side facing -x", StringComparison.Ordinal) && !region.Name.StartsWith("emissive", StringComparison.Ordinal));
            return (faces.Reference.X, faces.Frame.X);
        }
    }
}
