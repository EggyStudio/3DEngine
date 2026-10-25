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
        // they were drawn.
        Open();
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
}
