using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws frames offscreen and reads the order the model pass drew them in. The opaque draws are
/// bound by their pipeline, a draw that writes no depth keeps its place among them, and each is
/// drawn whatever the order.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ModelOrderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-order-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("order test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void Opaque_Draws_Through_A_Programs_Shader_Are_Bound_By_Their_Pipeline_And_Not_By_Turns()
    {
        // Sixteen cubes of their own drawn through DrawMesh with a program's shader, solid and cut
        // out by turns, as a voxel game's sections drew their solid and cut-out faces, each a batch
        // alone and the two kinds two pipelines, which the pass bound sixteen times in the order
        // they were drawn, and the sun's cascades bind by turns.
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.4f)), Color.White, 1, castsShadows: true);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(0.2, 0.6, 0.8, 1.0);
            }
            """, "flat.slang");
        var cubes = Enumerable.Range(0, 16).Select(_ => GenMeshCube(0.5f, 0.5f, 0.5f)).ToArray();
        var solid = new ModelMaterial(Color.White) { AlphaMode = MaterialAlphaMode.Opaque, Shader = shader };
        var cut = solid with { AlphaMode = MaterialAlphaMode.Mask };
        Vector3 At(int i) => new(i % 4 - 1.5f, i / 4 - 1.5f, 0);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            for (int i = 0; i < cubes.Length; i++) DrawMesh(cubes[i], i % 2 == 0 ? solid : cut, Matrix4x4.CreateTranslation(At(i)));
            EndMode3D();
        }, "turns");

        var models = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>();
        models.PipelinesBound.Should().Be(2, "the solid cubes are drawn together and the cut-out ones together");
        // Every pipeline of the passes takes the push in one range, so each pass pushes its view
        // once, where each cascade pushed it at all sixteen pipelines bound.
        models.CallsByPass.Keys.Should().Contain("cascade 0", "the sun's cascades draw the cubes");
        models.ViewsPushed.Should().BeLessThan(16, "each pass pushes its view once, not at every pipeline it binds");
        for (int i = 0; i < cubes.Length; i++)
        {
            var seen = GetWorldToScreenEx(At(i), _camera, 160, 120);
            GetImageColor(image, (int)seen.X, (int)seen.Y).B.Should().BeInRange(200, 208, $"cube {i} is drawn whatever its place in the order");
        }
        foreach (var cube in cubes) UnloadMesh(cube);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Draw_That_Writes_No_Depth_Keeps_Its_Place_Among_The_Opaque_Draws()
    {
        // A cube on the left, a sky around the camera with depth writes off, as raylib's skyboxes
        // are drawn, and a cube in the middle after it. The two cubes share a pipeline, and the sky,
        // near the eye, covers what was drawn before it, so the middle cube stands only if it is
        // still drawn after the sky.
        Open();
        var (left, sky, middle) = (GenMeshCube(1, 1, 1), GenMeshCube(1, 1, 1), GenMeshCube(1, 1, 1));
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawMesh(left, new ModelMaterial(Color.Green) { AlphaMode = MaterialAlphaMode.Opaque }, Matrix4x4.CreateTranslation(-1.5f, 0, 0));
            rlDisableDepthMask();
            DrawMesh(sky, new ModelMaterial(Color.Blue) { AlphaMode = MaterialAlphaMode.Opaque, DoubleSided = true }, Matrix4x4.CreateTranslation(_camera.Position));
            rlEnableDepthMask();
            DrawMesh(middle, new ModelMaterial(Color.Red) { AlphaMode = MaterialAlphaMode.Opaque }, Matrix4x4.Identity);
            EndMode3D();
        }, "sky");

        var (x, y) = (80, 60);
        var c = GetImageColor(image, x, y);
        c.R.Should().BeGreaterThan(200, "the middle cube is drawn after the sky, as it was recorded");
        c.B.Should().BeLessThan(100, "the sky does not cover the cube drawn after it");
        var leftAt = GetWorldToScreenEx(new Vector3(-1.5f, 0, 0), _camera, 160, 120);
        GetImageColor(image, (int)leftAt.X, (int)leftAt.Y).B.Should().BeGreaterThan(200, "the sky covers the cube drawn before it");
        foreach (var mesh in new[] { left, sky, middle }) UnloadMesh(mesh);
    }

    [NeedsVulkanFact]
    public void A_Frame_Pushes_And_Finds_Each_View_As_Often_However_Many_Mesh_Entities_It_Draws()
    {
        // Mesh entities each of a mesh of its own and a program's shader that keeps its positions,
        // solid and cut out by turns, as a voxel game's sections are, under the sun's cascades and
        // a water's mirror, 71,680 units out. Each pass pushes each view it draws through once and
        // finds it among those worked against the origin once, so a frame of 48 counts what one of
        // 16 does, where each batch looked its view up by its matrix and each pipeline bound
        // pushed it again.
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.4f)), Color.White, 1, castsShadows: true);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(0.2, 0.6, 0.8, 1.0);
            }
            """, "flat.slang");
        SetShaderKeepsPositions(shader, true);
        var at = new Vector3(71680, 0, 17408);
        var camera = new Camera3D(at + new Vector3(0, 6, 10), at, Vector3.UnitY, 45);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var meshes = new List<ModelMesh>();
        void Spawn(int count)
        {
            for (int i = meshes.Count, end = meshes.Count + count; i < end; i++)
            {
                var mesh = GenMeshCube(0.5f, 0.5f, 0.5f);
                meshes.Add(mesh);
                var entity = ecs.Spawn();
                ecs.Add(entity, new Mesh(mesh));
                ecs.Add(entity, new Material(Vector4.One) { Shader = shader, AlphaMode = i % 2 == 0 ? MaterialAlphaMode.Opaque : MaterialAlphaMode.Mask });
                ecs.Add(entity, new Transform(at + new Vector3(i % 8 - 3.5f, 0.25f, i / 8 - 3)));
            }
        }
        var models = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>();
        (int Pushed, int Found, int Drawn) Counted()
        {
            for (int frame = 0; frame < 4; frame++)
            {
                SetReflectionPlane(-0.25f, Vector3.UnitY);
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                EndMode3D();
                EndDrawing();
            }
            return (models.ViewsPushed, models.ViewLookups, models.DrawCalls);
        }
        Spawn(16);
        var few = Counted();
        Spawn(32);
        var many = Counted();

        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        models.CallsByPass.Keys.Should().Contain("cascade 0", "the sun's cascades draw the entities");
        many.Drawn.Should().BeGreaterThan(few.Drawn, "the frame draws the entities added");
        many.Pushed.Should().Be(few.Pushed, "each pass pushes each view it draws through once, however many batches it draws");
        many.Found.Should().Be(few.Found, "each pass finds each view it draws through once, however many batches ask for it");
        foreach (var mesh in meshes) UnloadMesh(mesh);
        UnloadShader(shader);
    }
}
