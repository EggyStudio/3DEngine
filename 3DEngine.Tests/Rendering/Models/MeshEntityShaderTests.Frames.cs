using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

// Mesh entities drawn in whole frames of an app, whose Render stage carries the transforms down the
// tree and gathers the draws, in the collection of the tests whose apps hold the process's one
// interface context.
public sealed partial class MeshEntityShaderTests
{
    private static readonly Vector3[] FrameTriangle = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];

    private static int ChildOf(EcsWorld ecs, int parent, Vector3 at)
    {
        var child = ecs.Spawn();
        ecs.Add(child, new Mesh(FrameTriangle));
        ecs.Add(child, new Material(new Vector4(1, 1, 1, 1)));
        ecs.Add(child, new Transform(at));
        ecs.SetParent(child, parent);
        return child;
    }

    [Fact]
    public void A_Child_Mesh_Is_Drawn_Where_Its_Parent_Is_This_Frame_And_So_Is_One_Spawned_Under_It_This_Frame()
    {
        var app = new App(Config.Default with { Headless = true, HeadlessFps = 1000 }).AddPlugin(new DefaultPlugins());
        // Where the frame's mesh entities are drawn, read as the draws are gathered, before a run
        // with nothing to render lets them go.
        var drawn = new List<Vector3>();
        app.AddSystem(Stage.Render, new SystemDescriptor(world =>
            {
                drawn.Clear();
                foreach (var group in world.Resource<ModelDrawList>().Groups)
                    foreach (var instance in group.ToArray())
                        drawn.Add(new Vector3(instance.WorldX.W, instance.WorldY.W, instance.WorldZ.W));
            }, "MeshEntityShaderTests.Drawn").After("RenderPlugin.MeshEntityDraws").MainThreadOnly());
        try
        {
            var ecs = app.World.Resource<EcsWorld>();
            var camera = ecs.Spawn();
            ecs.Add(camera, new Camera(60f));
            ecs.Add(camera, new Transform(new Vector3(0, 0, 5)));
            var parent = ecs.Spawn();
            ecs.Add(parent, new Transform(Vector3.Zero));
            ChildOf(ecs, parent, new Vector3(1, 0, 0));
            app.BeginFrame();
            app.EndFrame();
            drawn.Should().Equal([new Vector3(1, 0, 0)], "a child stands at its place under its parent");

            ecs.GetRef<Transform>(parent).Position = new Vector3(2, 0, 0);
            app.BeginFrame();
            app.EndFrame();
            drawn.Should().Equal([new Vector3(3, 0, 0)], "a child is drawn where its parent's move this frame puts it");

            ChildOf(ecs, parent, new Vector3(0, 2, 0));
            app.BeginFrame();
            app.EndFrame();
            drawn.Should().BeEquivalentTo([new Vector3(3, 0, 0), new Vector3(2, 2, 0)],
                "a child spawned this frame is drawn under its parent, and not at its own transform as though it were the world's");
        }
        finally
        {
            app.Shutdown();
        }
    }

    [Fact]
    public void Every_Draw_Render_Gathers_Runs_After_The_Transforms_Are_Carried_Down_The_Tree()
    {
        var app = new App(Config.Default with { Headless = true, HeadlessFps = 1000 }).AddPlugin(new DefaultPlugins());
        try
        {
            app.BeginFrame();
            app.EndFrame();
            var render = app.Schedule.SystemNames(Stage.Render).ToList();
            var carried = render.IndexOf("EcsPlugin.TransformPropagation");
            carried.Should().BeGreaterThanOrEqualTo(0, "the ECS carries the transforms down the tree in Render");
            foreach (var draws in (string[])["RenderPlugin.MeshEntityDraws", "RenderPlugin.AnimatedModelDraws", "RenderPlugin.EditMeshDraws"])
                render.IndexOf(draws).Should().BeGreaterThan(carried, $"{draws} reads the world matrices the carry writes in the same frame");
        }
        finally
        {
            app.Shutdown();
        }
    }
}
