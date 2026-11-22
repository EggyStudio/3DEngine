using FluentAssertions;
using System.Numerics;
using static Engine.E3D;

namespace Engine.Tests.DiagnosticsHost;

/// <summary>
/// The console and the settings panel holding the program's input while either is open, a key,
/// the pointer, the wheel and a pad's stick and buttons read as idle through the flat API and the
/// input resource alike and live with the debug overlay alone, the keys of the panels still acting,
/// and a key, a stick and a button held as the panel closes kept from the program until let go,
/// in a headless run.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class HeldInputTests : IDisposable
{
    public HeldInputTests() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static DiagnosticsBinding Binding => DiagnosticsBinding.Of(GetApp().World);

    private static Input Input => GetApp().World.Resource<Input>();

    private static void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            EndDrawing();
        }
    }

    // A key held through a frame and let go in the next.
    private static void Tap(Key key)
    {
        Input.SetKey(key, true);
        Frames(1);
        Input.SetKey(key, false);
        Frames(1);
    }

    // The pad's right stick and its right thumb button, which ImGui moves no row of a panel by.
    private static (GamepadState Pad, int Index) Pad()
    {
        var pad = Input.ConnectGamepad(41, "Pad", 0);
        return (pad, Input.Gamepads.ToList().IndexOf(pad));
    }

    // What the program reads of the key, the pointer, the wheel and the pad, through the flat API
    // and the input resource the systems and behaviors read.
    private static void ShouldRead(bool live, int pad, string because)
    {
        IsKeyDown(Key.W).Should().Be(live, because);
        Input.KeyDown(Key.W).Should().Be(live, because);
        IsKeyPressed(Key.W).Should().Be(live, because);
        GetMouseDelta().Should().Be(live ? new Vector2(5, 0) : Vector2.Zero, because);
        Input.MouseDelta.Should().Be(live ? (5, 0) : (0, 0), because);
        GetMouseWheelMove().Should().Be(live ? 1 : 0, because);
        GetGamepadAxisMovement(pad, GamepadAxis.RightX).Should().Be(live ? 0.8f : 0f, because);
        IsGamepadButtonDown(pad, GamepadButton.RightThumb).Should().Be(live, because);
        IsGamepadButtonPressed(pad, GamepadButton.RightThumb).Should().Be(live, because);
    }

    // The key, the pointer, the wheel and the pad moved as a frame's input comes in.
    private static void Move(GamepadState pad)
    {
        Input.SetKey(Key.W, true);
        Input.AddMouseDelta(5, 0);
        Input.SetMousePosition(400, 300);
        Input.AddWheel(0, 1);
        pad.SetAxis(GamepadAxis.RightX, 0.8f);
        pad.SetButton(GamepadButton.RightThumb, true);
    }

    // The key, the stick and the button let go, and the frame they are let go in.
    private static void LetGo(GamepadState pad)
    {
        Input.SetKey(Key.W, false);
        pad.SetAxis(GamepadAxis.RightX, 0);
        pad.SetButton(GamepadButton.RightThumb, false);
        Frames(1);
    }

    [Fact]
    public void A_Key_The_Pointer_And_A_Pad_Read_As_Idle_With_The_Panel_Open_And_Live_With_The_Overlay_Alone()
    {
        Frames(1);
        var (pad, index) = Pad();
        Input.SetMousePosition(200, 150);

        Tap(Key.F3);
        Binding.Keys.Panels.HoldsInput.Should().BeFalse("the overlay only shows");
        Move(pad);
        ShouldRead(live: true, index, "the overlay alone holds nothing from the program");
        GetMousePosition().Should().Be(new Vector2(400, 300), "the pointer moves with the overlay shown");
        LetGo(pad);
        Tap(Key.F3);

        Input.SetMousePosition(200, 150);
        Tap(Key.F5);
        Frames(1);
        Binding.Panel.Open.Should().BeTrue("F5 opens the settings panel");
        Binding.Keys.Panels.HoldsInput.Should().BeTrue("the panel takes input");
        Move(pad);
        ShouldRead(live: false, index, "the open panel holds the program's input");
        GetMousePosition().Should().Be(new Vector2(200, 150), "the pointer stays where it was as the panel opened");
        IsKeyReleased(Key.W).Should().BeFalse("no key comes up for the program while the panel is open");
        Input.KeysDown.Should().Contain(Key.W, "the engine's own readers, the panels' keys among them, see the key");
        pad.Axis(GamepadAxis.RightX).Should().Be(0.8f, "ImGui is given the pad as it stands");
        LetGo(pad);

        Tap(Key.F5);
        Binding.Panel.Open.Should().BeFalse("F5 still closes the panel, whose keys the hold leaves to them");
        Move(pad);
        ShouldRead(live: true, index, "the program's input is its own again once the panel closes");
        LetGo(pad);
    }

    [Fact]
    public void A_Key_A_Stick_And_A_Button_Held_As_The_Panel_Closes_Reach_The_Program_Only_Once_Let_Go()
    {
        Frames(1);
        var (pad, index) = Pad();
        Tap(Key.F5);
        Frames(1);
        Binding.Keys.Panels.HoldsInput.Should().BeTrue("F5 opens the settings panel");
        Move(pad);
        Frames(1);

        // The panel closes as its own back closes it at its first page, the key still held, which
        // F5 would not, since the panels' keys are taken alone.
        Binding.Panel.Open = false;
        Frames(1);
        Binding.Keys.Panels.HoldsInput.Should().BeFalse("the panel closed");
        Input.KeysDown.Should().Contain(Key.W, "the key is still held");
        IsKeyDown(Key.W).Should().BeFalse("a key held as the panel closes is no key of the program's");
        IsKeyPressed(Key.W).Should().BeFalse("nor a press of it");
        GetGamepadAxisMovement(index, GamepadAxis.RightX).Should().Be(0, "a stick held as the panel closes reads at rest");
        IsGamepadButtonDown(index, GamepadButton.RightThumb).Should().BeFalse("a button held as it closes reads as up");
        GetGamepadButtonPressed().Should().BeNull("nor is it the last button pressed");

        // A stick coming back near rest is read again, and pushed again it moves the program.
        pad.SetAxis(GamepadAxis.RightX, 0.1f);
        GetGamepadAxisMovement(index, GamepadAxis.RightX).Should().Be(0.1f, "the stick came back near rest");
        pad.SetAxis(GamepadAxis.RightX, 0.9f);
        GetGamepadAxisMovement(index, GamepadAxis.RightX).Should().Be(0.9f, "the stick is pushed again");

        Input.SetKey(Key.W, false);
        pad.SetButton(GamepadButton.RightThumb, false);
        IsKeyReleased(Key.W).Should().BeFalse("the program never saw the key go down");
        IsGamepadButtonReleased(index, GamepadButton.RightThumb).Should().BeFalse("nor the button");
        Frames(1);

        Input.SetKey(Key.W, true);
        pad.SetButton(GamepadButton.RightThumb, true);
        IsKeyPressed(Key.W).Should().BeTrue("pressed again, the key is the program's");
        IsKeyDown(Key.W).Should().BeTrue("and held");
        IsGamepadButtonPressed(index, GamepadButton.RightThumb).Should().BeTrue("as is the button");
        GetGamepadButtonPressed().Should().Be(GamepadButton.RightThumb, "which is the last pressed");
        LetGo(pad);
    }
}
