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

    // What the frame drew for mesh entities, as draws: each opaque entity's instance with its
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

        // Replaced by Add, which marks no change, the material still reaches the draw.
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
    public void Nothing_Is_Drawn_Without_A_Camera_Entity()
    {
        var (world, ecs) = Scene(camera: false);
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);

        MeshEntityDraws.Run(world);

        Drawn(world).Should().BeEmpty();
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
        var assets = new Assets<Texture>();
        world.InsertResource(assets);
        var handle = new Handle<Texture>(AssetId.Next(), new AssetPath("wood.png"), strong: false);
        var pixels = new byte[2 * 2 * 4 + 4]; // a 2x2 level and a 1x1 mip
        pixels[0] = 9;
        assets.Set(handle.Id, new Texture { Pixels = pixels, Width = 2, Height = 2, MipCount = 2, Format = TextureFormat.Rgba8 });
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
