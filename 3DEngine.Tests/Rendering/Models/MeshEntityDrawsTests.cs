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

        var draw = world.Resource<ModelDrawList>().Draws.Should().ContainSingle().Subject;
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
        draws.Draws.Should().ContainSingle().Which.Color.Should().Be(new Color(0, 0, 255, 255), "the material changed");
        draws.Draws[0].World.Translation.Should().Be(new Vector3(3, 0, 0), "the world matrix is the frame's");

        Frame();
        draws.Draws.Should().ContainSingle().Which.Color.Should().Be(new Color(0, 0, 255, 255), "the kept draw holds the new material");

        ecs.Despawn(entity);
        var again = SpawnMesh(ecs, Triangle, Vector3.Zero, new Vector4(0, 1, 0, 1));
        again.Should().Be(entity, "the id is given out again");
        Frame();
        draws.Draws.Should().ContainSingle().Which.Color.Should().Be(new Color(0, 255, 0, 255), "a new entity on the old id draws with its own material");

        // Replaced by Add, which marks no change, the material still reaches the draw.
        ecs.Add(again, new Material(new Vector4(1, 1, 1, 1)));
        Frame();
        draws.Draws.Should().ContainSingle().Which.Color.Should().Be(new Color(255, 255, 255, 255));
    }

    [Fact]
    public void Nothing_Is_Drawn_Without_A_Camera_Entity()
    {
        var (world, ecs) = Scene(camera: false);
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);

        MeshEntityDraws.Run(world);

        world.Resource<ModelDrawList>().Draws.Should().BeEmpty();
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
        world.Resource<ModelDrawList>().Draws.Should().HaveCount(4);
    }

    [Fact]
    public void A_Despawned_Mesh_Is_Freed_The_Next_Frame()
    {
        var (world, ecs) = Scene();
        var entity = SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);
        MeshEntityDraws.Run(world);
        var id = world.Resource<ModelDrawList>().Draws[0].Mesh;

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
        world.Resource<ModelDrawList>().Draws.Should().OnlyContain(d => d.Texture == upload.Id);
    }

    [Fact]
    public void With_No_Window_The_Camera_Takes_The_Shape_Of_The_Configured_Size()
    {
        var (world, ecs) = Scene();
        world.InsertResource(Config.Default.WithWindow("offscreen", 800, 400));
        SpawnMesh(ecs, Triangle, Vector3.Zero, Vector4.One);

        MeshEntityDraws.Run(world);

        // The camera has no rotation, so the projection's scales show through the view's translation.
        var vp = world.Resource<ModelDrawList>().Draws[0].ViewProjection;
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

        world.Resource<ModelDrawList>().Draws.Select(d => d.World.Translation.Z).Should().Equal(0, -3, 2);
    }
}
