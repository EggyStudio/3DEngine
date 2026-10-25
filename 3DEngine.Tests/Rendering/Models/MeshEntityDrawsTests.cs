using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

/// <summary>Mesh entities recorded into the model pass's draw list, without a GPU.</summary>
[Trait("Category", "Unit")]
public class MeshEntityDrawsTests
{
    private static readonly Vector3[] Triangle = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];

    private static (World World, EcsWorld Ecs) Scene(bool camera = true)
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        world.InitResource<ModelDrawList>();
        world.InitResource<MeshStore>();
        world.InitResource<TextureStore>();
        if (camera)
        {
            var cam = ecs.Spawn();
            ecs.Add(cam, new Camera(60f));
            ecs.Add(cam, new Transform(new Vector3(0, 0, 5)));
        }
        return (world, ecs);
    }

    // What the frame drew for mesh entities, as draws, each opaque entity's instance with its
    // group's template, its world matrix read back from the instance's rows and its color encoded
    // to sRGB again, then the translucent draws in the order recorded.
    private static List<ModelDraw> Drawn(World world)
    {
        var list = world.Resource<ModelDrawList>();
        var drawn = new List<ModelDraw>();
        foreach (var group in list.Groups)
            foreach (var instance in group.ToArray())
            {
                var world4 = new Matrix4x4(
                    instance.WorldX.X, instance.WorldY.X, instance.WorldZ.X, 0,
                    instance.WorldX.Y, instance.WorldY.Y, instance.WorldZ.Y, 0,
                    instance.WorldX.Z, instance.WorldY.Z, instance.WorldZ.Z, 0,
                    instance.WorldX.W, instance.WorldY.W, instance.WorldZ.W, 1);
                drawn.Add(group.Template with { World = world4, Color = Encoded(instance.Color) });
            }
        drawn.AddRange(list.Draws);
        return drawn;
    }

    private static Color Encoded(Vector4 linear)
    {
        static byte Byte(float c) => (byte)((c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f) * 255 + 0.5f);
        return new Color(Byte(linear.X), Byte(linear.Y), Byte(linear.Z), (byte)(linear.W * 255 + 0.5f));
    }

    private static int SpawnMesh(EcsWorld ecs, Vector3[] positions, Vector3 at, Vector4 albedo)
    {
        var entity = ecs.Spawn();
        ecs.Add(entity, new Mesh(positions));
        ecs.Add(entity, new Material(albedo));
        ecs.Add(entity, new Transform(at));
        return entity;
    }

    [Fact]
    public void A_Mesh_Entity_Is_Drawn_With_Its_Transform_And_Color()
    {
        var (world, ecs) = Scene();
        SpawnMesh(ecs, Triangle, new Vector3(2, 0, 0), new Vector4(1, 0.5f, 0, 1));

        MeshEntityDraws.Run(world);

        var draw = Drawn(world).Should().ContainSingle().Subject;
        draw.World.Translation.Should().Be(new Vector3(2, 0, 0));
        draw.Color.Should().Be(new Color(255, 188, 0, 255), "albedo is linear, and half of it is sRGB 188");
        draw.Texture.Should().Be(0);
        world.Resource<MeshStore>().Contains(draw.Mesh).Should().BeTrue();
    }

    [Fact]
    public void A_Kept_Draw_Follows_A_Changed_Material_A_Moved_Entity_And_A_Reused_Id()
    {
        var (world, ecs) = Scene();
        var entity = SpawnMesh(ecs, Triangle, Vector3.Zero, new Vector4(1, 0, 0, 1));
        var draws = world.Resource<ModelDrawList>();
        void Frame()
        {
            draws.Clear();
            TransformPropagation.Run(world);
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        Frame();

        ecs.Update(entity, new Material(new Vector4(0, 0, 1, 1)));
        ecs.GetRef<Transform>(entity).Position = new Vector3(3, 0, 0);
        Frame();
        Drawn(world).Should().ContainSingle().Which.Color.Should().Be(new Color(0, 0, 255, 255), "the material changed");
        Drawn(world)[0].World.Translation.Should().Be(new Vector3(3, 0, 0), "the world matrix is the frame's");

        Frame();
        Drawn(world).Should().ContainSingle().Which.Color.Should().Be(new Color(0, 0, 255, 255), "the kept draw holds the new material");

        ecs.Despawn(entity);
        var again = SpawnMesh(ecs, Triangle, Vector3.Zero, new Vector4(0, 1, 0, 1));
        again.Should().Be(entity, "the id is given out again");
        Frame();
        Drawn(world).Should().ContainSingle().Which.Color.Should().Be(new Color(0, 255, 0, 255), "a new entity on the old id draws with its own material");

        // Replaced by Add, which marks it changed, the material reaches the draw.
        ecs.Add(again, new Material(new Vector4(1, 1, 1, 1)));
        Frame();
        Drawn(world).Should().ContainSingle().Which.Color.Should().Be(new Color(255, 255, 255, 255));
    }

    [Fact]
    public void Opaque_Entities_Sharing_A_Mesh_And_Maps_Are_One_Group_Each_With_Its_Own_Color()
    {
        var (world, ecs) = Scene();
        SpawnMesh(ecs, Triangle, new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
        SpawnMesh(ecs, Triangle, new Vector3(2, 0, 0), new Vector4(0, 1, 0, 1));
        var single = SpawnMesh(ecs, Triangle, new Vector3(3, 0, 0), new Vector4(0, 0, 1, 1));
        ecs.GetRef<Material>(single).DoubleSided = false;

        MeshEntityDraws.Run(world);

        var list = world.Resource<ModelDrawList>();
        list.Draws.Should().BeEmpty("opaque entities are recorded as instances");
        list.Groups.Select(g => g.Count).Should().Equal(2, 1);
        list.Groups[0].ToArray().Select(i => i.Color).Should().Equal(new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1));
        list.Groups[1].Template.DoubleSided.Should().BeFalse("a single-sided material is drawn by another pipeline");
        list.WindowViewProjection.Should().Be(list.Groups[0].Template.ViewProjection, "the shadow is fitted to the camera the groups were recorded through");
    }

    [Fact]
    public void Entities_Past_A_Chunk_Are_Recorded_On_Threads_And_Each_Drawn_Once_Where_It_Is()
    {
        var (world, ecs) = Scene();
        Vector3[] other = [new(0, 0, 0), new(0, 1, 0), new(0, 0, 1)];
        void Spawn(int from, int to)
        {
            for (int i = from; i < to; i++)
                SpawnMesh(ecs, i % 5 == 0 ? other : Triangle, new Vector3(i, 0, 0),
                    i % 7 == 0 ? new Vector4(0, 0, 1, 0.5f) : new Vector4(i % 3 == 0 ? 1 : 0, 1, 0, 1));
        }
        var draws = world.Resource<ModelDrawList>();
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
        }

        Spawn(0, 10_000);
        Frame();
        // Spawned between frames, with meshes and looks of their own, so chunks leave them for after.
        Vector3[] third = [new(0, 0, 0), new(1, 1, 0), new(0, 0, 1)];
        for (int i = 10_000; i < 10_100; i++) SpawnMesh(ecs, third, new Vector3(i, 0, 0), new Vector4(0.25f, 0.5f, 0.75f, 1));
        Spawn(10_100, 12_000);
        Frame();

        var drawn = Drawn(world);
        drawn.Select(d => d.World.Translation.X).Order().Should().Equal(Enumerable.Range(0, 12_000).Select(i => (float)i), "each entity is drawn once");
        draws.Draws.Should().HaveCount(Enumerable.Range(0, 12_000).Count(i => (i < 10_000 || i >= 10_100) && i % 7 == 0));
        draws.Draws.Select(d => Vector3.DistanceSquared(d.World.Translation, new Vector3(0, 0, 5))).Should().BeInDescendingOrder("translucent draws go from far to near");
        drawn.Where(d => d.World.Translation.X is >= 10_000 and < 10_100).Should().OnlyContain(d => d.Color == new Color(137, 188, 225, 255));
        world.Resource<MeshEntityDraws>().MeshCount.Should().Be(3);
    }

    [Fact]
    public void Looks_Past_Four_Thousand_Are_Kept_From_Frame_To_Frame()
    {
        // Five thousand entities in two chunks, each of its own color. Past 4,096 looks every look
        // was forgotten and built again each frame, and no chunk was kept.
        var (world, ecs) = Scene();
        for (int i = 0; i < 5000; i++) SpawnMesh(ecs, Triangle, new Vector3(i, 0, 0), new Vector4(i / 5000f, 1, 0, 1));
        var draws = world.Resource<ModelDrawList>();
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        for (int frame = 0; frame < 3; frame++)
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        self.LookCount.Should().Be(5000);
        self.ChunksKept.Should().Be(2, "no look was forgotten, so both chunks keep what they gathered");
        Drawn(world).Should().HaveCount(5000);
    }

    [Fact]
    public void A_Mesh_Is_Kept_While_Its_Entity_Holds_It_And_One_Replaced_Leaves_The_Other_Chunks_As_They_Were()
    {
        var (world, ecs) = Scene();
        for (int i = 0; i < 5000; i++) SpawnMesh(ecs, Triangle, new Vector3(i, 0, 0), new Vector4(0, 1, 0, 1));
        Vector3[] own = [new(0, 0, 0), new(2, 0, 0), new(0, 2, 0)];
        var apart = SpawnMesh(ecs, own, new Vector3(-1, 0, 0), new Vector4(1, 0, 0, 1));
        var draws = world.Resource<ModelDrawList>();
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        for (int frame = 0; frame < 3; frame++) Frame();
        self.MeshCount.Should().Be(2);

        ecs.Remove<Material>(apart);
        Frame();
        self.MeshCount.Should().Be(2, "an entity drawn without a material still holds its mesh");
        ecs.Add(apart, new Material(new Vector4(1, 0, 0, 1)));

        ecs.Update(apart, new Mesh([new(0, 0, 0), new(3, 0, 0), new(0, 3, 0)]));
        Frame();
        self.MeshCount.Should().Be(2, "the replaced mesh is freed and its replacement uploaded");
        Frame();
        self.ChunksKept.Should().Be(1, "the chunk of the entity whose mesh was replaced is gathered again, the other kept as it was");
        self.LookCount.Should().Be(2, "the replaced mesh's look went with it");

        var camera = ecs.Query<Camera>().Single().Entity;
        ecs.Despawn(camera);
        Frame();
        self.MeshCount.Should().Be(2, "a frame with no camera lets no mesh go");
    }

    [Fact]
    public void An_Entity_Draws_A_Mesh_The_Program_Uploaded_And_Its_Looks_Go_When_No_Entity_Holds_It()
    {
        var (world, ecs) = Scene();
        var store = world.Resource<MeshStore>();
        var id = store.Add([new ModelVertex(new Vector3(0, 0, 0), Vector3.UnitZ, default), new ModelVertex(new Vector3(4, 0, 0), Vector3.UnitZ, default),
            new ModelVertex(new Vector3(0, 4, 0), Vector3.UnitZ, default)], [0, 1, 2]);
        var uploaded = new ModelMesh(id, 3, 1, new BoundingBox(Vector3.Zero, new Vector3(4, 4, 0)));
        var entity = ecs.Spawn();
        ecs.Add(entity, new Mesh(uploaded));
        ecs.Add(entity, new Material(new Vector4(1, 1, 1, 1)));
        ecs.Add(entity, new Transform(new Vector3(1, 2, 0)));
        var draws = world.Resource<ModelDrawList>();
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }

        Frame();
        var drawn = Drawn(world).Should().ContainSingle().Subject;
        drawn.Mesh.Should().Be(id, "the program's mesh is drawn under its own id");
        drawn.World.Translation.Should().Be(new Vector3(1, 2, 0));
        draws.Groups.Single(g => g.Count > 0).Sphere.Radius.Should().BeApproximately(MathF.Sqrt(8), 1e-5f, "the group is culled by a sphere about the mesh's bounds");
        self.MeshCount.Should().Be(0, "the engine uploads nothing of its own for it");
        self.LookCount.Should().Be(1);

        ecs.Despawn(entity);
        Frame();
        self.LookCount.Should().Be(0, "no entity holds the program's mesh");
        store.TryGetData(id, out _, out _).Should().BeTrue("the program frees its mesh itself");
    }

    [Fact]
    public void A_Mesh_Of_Indexed_Vertices_Is_Uploaded_Once_With_Its_Triangles()
    {
        var (world, ecs) = Scene();
        Vector3[] square = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];
        var entity = ecs.Spawn();
        // The third triangle names a vertex the positions do not hold and is left out.
        ecs.Add(entity, new Mesh(square, [0, 1, 2, 0, 2, 3, 0, 2, 9]));
        ecs.Add(entity, new Material(new Vector4(1, 1, 1, 1)));
        MeshEntityDraws.Run(world);

        var mesh = Drawn(world).Should().ContainSingle().Subject.Mesh;
        world.Resource<MeshStore>().TryGetData(mesh, out var vertices, out var indices).Should().BeTrue();
        vertices.Select(v => v.Position).Should().Equal(square);
        indices.Should().Equal(0u, 1u, 2u, 0u, 2u, 3u);
        vertices.Should().OnlyContain(v => Vector3.Distance(v.Normal, Vector3.UnitZ) < 1e-5f, "a vertex with no normal of its own takes its faces'");
    }

    [Fact]
    public void Entities_Standing_Still_Are_Drawn_From_What_Was_Gathered_The_Frame_Before()
    {
        var (world, ecs) = Scene();
        var still = SpawnMesh(ecs, Triangle, new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
        var mover = SpawnMesh(ecs, Triangle, new Vector3(2, 0, 0), new Vector4(0, 1, 0, 1));
        var draws = world.Resource<ModelDrawList>();
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        Vector3[] Placed() => [.. Drawn(world).Select(d => d.World.Translation).Order(Comparer<Vector3>.Create((a, b) => a.X.CompareTo(b.X)))];

        Frame();
        self.ChunksKept.Should().Be(0, "the first frame gathers everything, leaving the mesh's upload to the main thread");
        Frame();
        self.ChunksKept.Should().Be(0, "a chunk that left entities for after is gathered again, this time whole");
        Frame();
        self.ChunksKept.Should().Be(1, "nothing moved, so the chunk keeps what it gathered");
        Placed().Should().Equal(new Vector3(1, 0, 0), new Vector3(2, 0, 0));

        ecs.GetRef<Transform>(mover).Position = new Vector3(5, 0, 0);
        Frame();
        self.ChunksKept.Should().Be(0, "an entity in the chunk moved");
        Placed().Should().Equal(new Vector3(1, 0, 0), new Vector3(5, 0, 0));

        ecs.Despawn(still);
        Frame();
        Placed().Should().Equal([new Vector3(5, 0, 0)], "a despawned entity is gone at once");
    }

    [Fact]
    public void An_Entity_Not_Visible_Is_Drawn_Nowhere_Keeping_Its_Mesh_And_One_Shadow_Only_Is_Drawn_Into_The_Shadow_Maps_Alone()
    {
        var (world, ecs) = Scene();
        var hidden = SpawnMesh(ecs, [.. Triangle], new Vector3(1, 0, 0), new Vector4(1, 0, 0, 1));
        var roof = SpawnMesh(ecs, Triangle, new Vector3(2, 0, 0), new Vector4(1, 0, 0, 1));
        ecs.Add(hidden, new Visible(true));
        ecs.Add(roof, new ShadowOnly(false));
        world.InitResource<FrameProfile>();
        var draws = world.Resource<ModelDrawList>();
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        void Frame()
        {
            draws.Clear();
            MeshEntityDraws.Run(world);
            ecs.BeginFrame();
        }
        (float X, bool ShadowOnly)[] Placed() => [.. Drawn(world).Select(d => (d.World.Translation.X, d.ShadowOnly)).OrderBy(p => p.X)];

        for (int i = 0; i < 3; i++) Frame();
        Placed().Should().Equal((1f, false), (2f, false));
        self.ChunksKept.Should().Be(1);

        ecs.GetRef<Visible>(hidden).Value = false;
        ecs.GetRef<ShadowOnly>(roof).Value = true;
        Frame();
        Placed().Should().Equal([(2f, true)], "the hidden entity is drawn nowhere and the roof into the shadow maps alone");
        self.ChunksKept.Should().Be(0, "a bool written in place has its chunk gathered again");
        self.MeshCount.Should().Be(2, "the hidden entity still holds its mesh");
        self.Drawn.Should().Be(1);
        world.Resource<FrameProfile>().Snapshot().Values.Should().Contain(("entities.drawn", 1d));

        Frame();
        self.ChunksKept.Should().Be(0, "the roof's new look was built after the chunks, so its chunk is gathered again whole");
        Frame();
        self.ChunksKept.Should().Be(1, "nothing changed since");
        ecs.GetRef<Visible>(hidden).Value = true;
        ecs.GetRef<ShadowOnly>(roof).Value = false;
        Frame();
        Placed().Should().Equal((1f, false), (2f, false));
        world.Resource<FrameProfile>().Snapshot().Values.Should().Contain(("entities.drawn", 2d));
    }

    [Fact]
    public void Nothing_Is_Drawn_Without_A_Camera_Entity()
    {
        var (world, ecs) = Scene(camera: false);
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);

        MeshEntityDraws.Run(world);

        Drawn(world).Should().BeEmpty();
    }

    [Fact]
    public void With_No_Camera_Entity_The_Window_Is_Drawn_Through_The_Camera_Of_BeginMode3D()
    {
        // As a program on the flat API that loads a scene of models has it.
        var (world, ecs) = Scene(camera: false);
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        var viewProjection = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
                             * Matrix4x4.CreatePerspectiveFieldOfView(1, 1, 0.1f, 100);
        world.InsertResource(new Mode3DCamera { ViewProjection = viewProjection, Eye = new Vector3(0, 0, 5) });

        MeshEntityDraws.Run(world);

        var draw = Drawn(world).Should().ContainSingle().Subject;
        draw.ViewProjection.Should().Be(viewProjection);
        draw.Target.Should().Be(0, "into the window");
    }

    [Fact]
    public void A_Camera_Entity_For_The_Window_Comes_Before_The_Camera_Of_BeginMode3D()
    {
        var (world, ecs) = Scene();
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        world.InsertResource(new Mode3DCamera { ViewProjection = Matrix4x4.Identity });

        MeshEntityDraws.Run(world);

        Drawn(world).Should().ContainSingle().Which.ViewProjection.Should().NotBe(Matrix4x4.Identity, "the camera entity draws the window");
    }

    [Fact]
    public void A_Mesh_Is_Uploaded_Once_And_Its_Normals_Face_The_Triangle()
    {
        var (world, ecs) = Scene();
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        SpawnMesh(ecs, Triangle, Vector3.One, Vector4.One);

        MeshEntityDraws.Run(world);
        MeshEntityDraws.Run(world);

        var upload = world.Resource<MeshStore>().Take().Uploads.Should().ContainSingle("both entities share one positions array").Subject;
        upload.Vertices.Select(v => v.Normal).Should().AllBeEquivalentTo(Vector3.UnitZ);
        Drawn(world).Should().HaveCount(4);
    }

    [Fact]
    public void A_Despawned_Mesh_Is_Freed_The_Next_Frame()
    {
        var (world, ecs) = Scene();
        var entity = SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        MeshEntityDraws.Run(world);
        var id = Drawn(world)[0].Mesh;

        ecs.Despawn(entity);
        MeshEntityDraws.Run(world);

        world.Resource<MeshStore>().Contains(id).Should().BeFalse();
        world.Resource<MeshEntityDraws>().MeshCount.Should().Be(0);
    }

    [Fact]
    public void A_Loaded_Base_Color_Texture_Is_Copied_Once_At_Its_First_Mip()
    {
        var (world, ecs) = Scene();
        var assets = new Assets<TextureAsset>();
        world.InsertResource(assets);
        var handle = new Handle<TextureAsset>(AssetId.Next(), new AssetPath("wood.png"), strong: false);
        var pixels = new byte[2 * 2 * 4 + 4]; // a 2x2 level and a 1x1 mip
        pixels[0] = 9;
        assets.Set(handle.Id, new TextureAsset { Pixels = pixels, Width = 2, Height = 2, MipCount = 2, Format = TextureFormat.Rgba8 });
        var entity = SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        ecs.GetRef<Material>(entity).BaseColorTexture = handle;

        MeshEntityDraws.Run(world);
        MeshEntityDraws.Run(world);

        var upload = world.Resource<TextureStore>().Take().Uploads.Should().ContainSingle().Subject;
        (upload.Width, upload.Height, upload.Rgba!.Length, upload.Rgba[0]).Should().Be((2, 2, 16, (byte)9));
        Drawn(world).Should().OnlyContain(d => d.Texture == upload.Id);
    }

    [Fact]
    public void With_No_Window_The_Camera_Takes_The_Shape_Of_The_Configured_Size()
    {
        var (world, ecs) = Scene();
        world.InsertResource(Config.Default.WithWindow("offscreen", 800, 400));
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);

        MeshEntityDraws.Run(world);

        // The camera has no rotation, so the projection's scales show through the view's translation.
        var vp = Drawn(world)[0].ViewProjection;
        (MathF.Abs(vp.M22) / MathF.Abs(vp.M11)).Should().BeApproximately(2f, 1e-4f, "an 800 by 400 frame is twice as wide as it is tall");
    }

    [Fact]
    public void Translucent_Entities_Are_Recorded_After_The_Opaque_Ones_From_Far_To_Near()
    {
        var (world, ecs) = Scene();
        // The camera is at z 5, so z -3 is farther than z 2.
        SpawnMesh(ecs, Triangle, new Vector3(0, 0, 2), new Vector4(1, 0, 0, 0.5f));
        SpawnMesh(ecs, Triangle, new Vector3(0, 0, 0), Vector4.One);
        SpawnMesh(ecs, Triangle, new Vector3(0, 0, -3), new Vector4(0, 0, 1, 0.5f));

        MeshEntityDraws.Run(world);

        Drawn(world).Select(d => d.World.Translation.Z).Should().Equal(0, -3, 2);
    }

    [Fact]
    public void A_Second_Camera_Draws_The_Same_Instances_Through_Its_Own_View_And_Order()
    {
        var (world, ecs) = Scene();
        // A camera behind the scene, at z -5 looking back, into a render texture, so the
        // translucent entities' order turns around for it.
        var screen = ecs.Spawn();
        ecs.Add(screen, new Camera(60f) { Target = new RenderTexture2D(new Texture2D(7, 64, 64)) });
        ecs.Add(screen, new Transform(new Vector3(0, 0, -5), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Vector3.One));
        SpawnMesh(ecs, Triangle, new Vector3(0, 0, 2), new Vector4(1, 0, 0, 0.5f));
        SpawnMesh(ecs, Triangle, new Vector3(1, 0, 0), Vector4.One);
        SpawnMesh(ecs, Triangle, new Vector3(0, 0, -3), new Vector4(0, 0, 1, 0.5f));

        MeshEntityDraws.Run(world);

        var drawn = Drawn(world);
        var window = drawn.Where(d => d.Target == 0).ToList();
        var texture = drawn.Where(d => d.Target == 7).ToList();
        window.Select(d => d.World.Translation.Z).Should().Equal(0, -3, 2);
        // From behind, z 2 is the farther.
        texture.Select(d => d.World.Translation.Z).Should().Equal(0, 2, -3);
        texture[0].ViewProjection.Should().NotBe(window[0].ViewProjection);
        texture.Should().OnlyContain(d => d.ViewProjection == texture[0].ViewProjection);
        world.Resource<ModelDrawList>().Groups.Select(g => g.ToArray()[0]).Distinct().Should().ContainSingle(
            "the opaque entity's one instance is shared by both cameras' groups");
    }
}
