using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Automation events recorded from a frame's input, written to a file and played back, as raylib's.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class Engine3DAutomationTests : IDisposable
{
    private readonly App _app = new App(Config.Default with { Headless = true, HeadlessFps = 240 }).AddPlugin(new DefaultPlugins());
    private readonly TestFolder _folder = new("engine-automation-");

    public Engine3DAutomationTests() => UseApp(_app);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private Input Input => _app.World.Resource<Input>();

    private static void Frame()
    {
        BeginDrawing();
        EndDrawing();
    }

    [Fact]
    public void A_Held_Key_Is_Recorded_Each_Frame_And_Its_Release_Once_From_The_Base_Frame()
    {
        var list = LoadAutomationEventList(null);
        SetAutomationEventList(list);
        SetAutomationEventBaseFrame(180);
        StartAutomationEventRecording();

        Input.SetKey(Key.Right, true);
        Frame();
        Frame();
        Input.SetKey(Key.Right, false);
        Input.SetMousePosition(5, 6);
        Frame();
        Frame();
        StopAutomationEventRecording();
        Input.SetKey(Key.Left, true);
        Frame();

        list.Events[..list.Count].Should().Equal(
            new AutomationEvent(180, AutomationEventType.InputKeyDown, (int)Key.Right),
            new AutomationEvent(181, AutomationEventType.InputKeyDown, (int)Key.Right),
            new AutomationEvent(182, AutomationEventType.InputKeyUp, (int)Key.Right),
            new AutomationEvent(182, AutomationEventType.InputMousePosition, 5, 6));
    }

    [Fact]
    public void A_List_Written_To_A_File_Reads_Back_The_Same()
    {
        var list = LoadAutomationEventList(null);
        SetAutomationEventList(list);
        StartAutomationEventRecording();
        Input.SetMouseButton(MouseButton.Left, true);
        Input.SetMousePosition(40, 30);
        Frame();
        Input.SetMouseButton(MouseButton.Left, false);
        Frame();
        StopAutomationEventRecording();

        var path = _folder.File("run.rae");
        ExportAutomationEventList(list, path).Should().BeTrue();
        // The press, the pointer, the tap the gestures read the press as, and the release
        File.ReadAllText(path).Should().Contain("c 4\n").And.Contain("// Event: INPUT_GESTURE").And.Contain("// Event: INPUT_MOUSE_BUTTON_UP");

        var read = LoadAutomationEventList(path);
        read.Count.Should().Be(4);
        read.Events[..read.Count].Should().Equal(list.Events[..list.Count]);
    }

    [Fact]
    public void A_List_To_Record_Into_Is_Empty_And_Holds_As_Many_As_Raylibs()
    {
        var list = LoadAutomationEventList(null);
        (list.Count, list.Capacity).Should().Be((0, 16384));

        var missing = LoadAutomationEventList(_folder.File("none.rae"));
        missing.Count.Should().Be(0, "a file that cannot be read gives an empty list, as raylib's does");
    }

    [Fact]
    public void A_Key_Played_Down_Is_Pressed_Then_Held_Until_Played_Up()
    {
        PlayAutomationEvent(new AutomationEvent(0, AutomationEventType.InputKeyDown, (int)Key.Space));
        (IsKeyDown(Key.Space), IsKeyPressed(Key.Space)).Should().Be((true, true), "a program reads what was played in the same frame");
        Frame();

        (IsKeyDown(Key.Space), IsKeyPressed(Key.Space)).Should().Be((true, false), "a key played down stays down, as a held key does");
        PlayAutomationEvent(new AutomationEvent(1, AutomationEventType.InputKeyUp, (int)Key.Space));
        (IsKeyDown(Key.Space), IsKeyReleased(Key.Space)).Should().Be((false, true));
    }

    [Fact]
    public void The_Pointer_And_The_Wheel_Are_Played_Where_They_Were()
    {
        Input.SetMousePosition(10, 10);
        PlayAutomationEvent(new AutomationEvent(0, AutomationEventType.InputMousePosition, 30, 50));
        PlayAutomationEvent(new AutomationEvent(0, AutomationEventType.InputMouseWheelMotion, 0, -2));

        GetMousePosition().Should().Be(new Vector2(30, 50));
        GetMouseDelta().Should().Be(new Vector2(20, 40));
        GetMouseWheelMove().Should().Be(-2);
    }

    [Fact]
    public void Nothing_Is_Played_While_Recording()
    {
        SetAutomationEventList(LoadAutomationEventList(null));
        StartAutomationEventRecording();
        PlayAutomationEvent(new AutomationEvent(0, AutomationEventType.InputKeyDown, (int)Key.Space));
        IsKeyDown(Key.Space).Should().BeFalse("raylib plays nothing while it records");
    }
}
