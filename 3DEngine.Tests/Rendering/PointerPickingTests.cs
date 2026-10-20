using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>
/// What the mouse does to a box a picking camera draws, triggered on the box and its parents, and
/// nothing cast where no camera asks.
/// </summary>
/// <remarks>
/// The picking reads whether ImGui wants the mouse from ImGui's one context for the process, so
/// these run in the collection of the tests that build one, never beside them.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Unit")]
public class PointerPickingTests
{
    // A window of 600 by 400 with a camera at z 10 looking down -z, its middle at (300, 200), and a
    // box two units wide at the origin, whose face toward the camera is at z 1.
    private sealed class Scene : IDisposable
    {
        public readonly App App = new(Config.Default);
        public readonly EcsWorld Ecs;
        public readonly Input Input;
        public readonly int Camera, Box, Holder;
        public readonly List<string> Told = [];

        public Scene(bool picking = true)
        {
            App.AddPlugin(new InputPlugin()).AddPlugin(new EcsPlugin());
            Ecs = App.World.Resource<EcsWorld>();
            Input = App.World.Resource<Input>();
            Camera = Ecs.Spawn();
            Ecs.Add(Camera, new Camera(60) { Picking = picking });
            Ecs.Add(Camera, new Transform(new Vector3(0, 0, 10)));
            Holder = Ecs.Spawn();
            Ecs.Add(Holder, Transform.Identity);
            Box = Ecs.Spawn();
            Ecs.Add(Box, Cube());
            Ecs.Add(Box, Transform.Identity);
            Ecs.SetParent(Box, Holder);
        }

        // A frame's input, then the stage the picking runs in, then what Last clears.
        public void Frame(int x, int y, MouseButton? down = null, MouseButton? up = null)
        {
            Input.SetMousePosition(x, y);
            if (down is { } pressed) Input.SetMouseButton(pressed, true);
            if (up is { } released) Input.SetMouseButton(released, false);
            App.Schedule.RunStage(Stage.PreUpdate, App.World);
            Input.BeginFrame();
        }

        public void Dispose() => App.Dispose();
    }

    private static Mesh Cube()
    {
        Vector3[] c = [new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1), new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)];
        int[] faces = [4, 5, 6, 4, 6, 7, 1, 0, 3, 1, 3, 2, 0, 4, 7, 0, 7, 3, 5, 1, 2, 5, 2, 6, 3, 7, 6, 3, 6, 2, 0, 1, 5, 0, 5, 4];
        return new Mesh([.. faces.Select(i => c[i])]);
    }

    private static string Say<T>(Pointer<T> pointer) where T : struct, IPointerEvent => pointer.Event switch
    {
        Over over => $"over {Place(over.Hit)}",
        Out => "out",
        Move move => $"move {move.Delta.X},{move.Delta.Y}",
        Press press => $"press {press.Button}",
        Release release => $"release {release.Button}",
        Click click => $"click {click.Button} {Place(click.Hit)}",
        DragStart start => $"drag start {start.Button}",
        Drag drag => $"drag {drag.Distance.X},{drag.Distance.Y} by {drag.Delta.X},{drag.Delta.Y}",
        DragEnd end => $"drag end {end.Distance.X},{end.Distance.Y}",
        _ => "?",
    };

    private static string Place(PointerHit hit) =>
        $"at {MathF.Round(hit.Point.X, 2)},{MathF.Round(hit.Point.Y, 2)},{MathF.Round(hit.Point.Z, 2)} facing {hit.Normal.Z} from {MathF.Round(hit.Distance, 2)}";

    private static void Observe<T>(Scene scene, int entity, string who) where T : struct, IPointerEvent =>
        scene.Ecs.Observe<Pointer<T>>(scene.Ecs.Handle(entity), on => scene.Told.Add($"{who} {Say(on.Event)}"));

    private static void ObserveAll(Scene scene, int entity, string who)
    {
        Observe<Over>(scene, entity, who);
        Observe<Out>(scene, entity, who);
        Observe<Move>(scene, entity, who);
        Observe<Press>(scene, entity, who);
        Observe<Release>(scene, entity, who);
        Observe<Click>(scene, entity, who);
        Observe<DragStart>(scene, entity, who);
        Observe<Drag>(scene, entity, who);
        Observe<DragEnd>(scene, entity, who);
    }

    [Fact]
    public void The_Pointer_Comes_Over_Moves_On_Presses_Clicks_And_Leaves_A_Box()
    {
        using var scene = new Scene();
        ObserveAll(scene, scene.Box, "box");

        scene.Frame(10, 10);
        scene.Frame(300, 200);
        scene.Frame(302, 200);
        scene.Frame(302, 200, down: MouseButton.Left);
        scene.Frame(302, 200, up: MouseButton.Left);
        scene.Frame(10, 10);

        scene.Told.Should().Equal(
            "box over at 0,0,1 facing 1 from 9",
            "box move 2,0",
            "box press Left",
            "box release Left",
            "box click Left at 0.05,0,1 facing 1 from 9",
            "box out");
    }

    [Fact]
    public void A_Drag_Started_On_The_Box_Is_Told_To_It_Wherever_The_Pointer_Goes_And_Ends_Without_A_Click_Off_It()
    {
        using var scene = new Scene();
        ObserveAll(scene, scene.Box, "box");

        scene.Frame(300, 200);
        scene.Frame(300, 200, down: MouseButton.Right);
        scene.Frame(310, 205);
        scene.Frame(500, 205);
        scene.Frame(500, 205, up: MouseButton.Right);

        scene.Told.Should().Equal(
            "box over at 0,0,1 facing 1 from 9",
            "box press Right",
            "box move 10,5",
            "box drag start Right",
            "box drag 10,5 by 10,5",
            "box out",
            "box drag 200,5 by 190,0",
            "box drag end 200,5");
    }

    [Fact]
    public void A_Pointer_Event_Climbs_To_The_Box_s_Parent_Until_An_Observer_Stops_It()
    {
        using var scene = new Scene();
        Observe<Click>(scene, scene.Box, "box");
        Observe<Click>(scene, scene.Holder, "holder");
        Observe<Press>(scene, scene.Holder, "holder");
        scene.Ecs.Observe<Pointer<Press>>(scene.Ecs.Handle(scene.Box), on => on.Propagate(false));

        scene.Frame(300, 200, down: MouseButton.Left);
        scene.Frame(300, 200, up: MouseButton.Left);

        scene.Told.Should().Equal("box click Left at 0,0,1 facing 1 from 9", "holder click Left at 0,0,1 facing 1 from 9");
    }

    [Fact]
    public void Nothing_Is_Cast_Where_No_Camera_Asks_To_Pick()
    {
        using var scene = new Scene(picking: false);
        ObserveAll(scene, scene.Box, "box");
        var picking = scene.App.World.Resource<PointerPicking>();

        for (int i = 0; i < 3; i++) scene.Frame(300, 200);
        var before = GC.GetAllocatedBytesForCurrentThread();
        PointerPicking.Run(scene.App.World);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        scene.Frame(300, 200, down: MouseButton.Left);
        scene.Frame(300, 200, up: MouseButton.Left);

        picking.Casts.Should().Be(0);
        allocated.Should().Be(0, "a frame with no camera asking reads the cameras alone");
        scene.Told.Should().BeEmpty();

        var camera = scene.Ecs.GetReadOnly<Camera>(scene.Camera);
        scene.Ecs.Update(scene.Camera, camera with { Picking = true });
        scene.Frame(300, 200);
        picking.Casts.Should().Be(1, "the camera asked");
        scene.Told.Should().Equal("box over at 0,0,1 facing 1 from 9");
    }
}
