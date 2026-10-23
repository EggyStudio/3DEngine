using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A headless program that draws every frame, whose draws nothing renders, letting them go at each
/// frame's end as a program with a window does, so its lists hold one frame's draws and no more.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class HeadlessDrawListTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    public void A_Headless_Program_Drawing_A_Hundred_Frames_Holds_One_Frames_Draws_And_None_Past_Its_End()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        var world = GetApp().World;
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 45);
        var during = new HashSet<(int Vertices, int Models)>();
        var after = new HashSet<(int Vertices, int Models)>();

        for (int frame = 0; frame < 100; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            DrawRectangle(10, 10, 40, 20, Color.Red);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 1, 1, 1, Color.Blue);
            DrawModel(cube, new Vector3(2, 0, 0), 1, Color.White);
            EndMode3D();
            during.Add((world.Resource<DrawList>().Vertices.Length, world.Resource<ModelDrawList>().Draws.Count));
            EndDrawing();
            after.Add((world.Resource<DrawList>().Vertices.Length, world.Resource<ModelDrawList>().Draws.Count));
        }

        during.Should().ContainSingle("each frame holds its own draws and none of the frames before it");
        during.Single().Models.Should().Be(1);
        after.Should().Equal([(0, 0)], "the draws are let go at each frame's end");
        UnloadModel(cube);
    }
}
