using FluentAssertions;

namespace Engine.Tests.Core;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class GamepadTests
{
    [Fact]
    public void A_Button_Is_Pressed_For_One_Frame_And_Down_Until_Released()
    {
        var input = new Input();
        var pad = input.ConnectGamepad(7, "Pad", 0);

        pad.SetButton(GamepadButton.South, true);
        pad.ButtonPressed(GamepadButton.South).Should().BeTrue();

        input.BeginFrame();
        pad.ButtonPressed(GamepadButton.South).Should().BeFalse();
        pad.ButtonDown(GamepadButton.South).Should().BeTrue();

        pad.SetButton(GamepadButton.South, false);
        pad.ButtonReleased(GamepadButton.South).Should().BeTrue();
        pad.ButtonDown(GamepadButton.South).Should().BeFalse();
    }

    [Fact]
    public void Press_Any_Button_The_Wheel_And_The_Pointer_Read_As_Raylibs_Do()
    {
        var app = new App();
        app.World.InitResource<Input>();
        Engine3D.UseApp(app);
        try
        {
            var input = app.World.Resource<Input>();
            var first = input.ConnectGamepad(1, "First", 0);
            var second = input.ConnectGamepad(2, "Second", 0);

            Engine3D.GetGamepadButtonPressed().Should().BeNull();
            second.SetButton(GamepadButton.Start, true);
            Engine3D.GetGamepadButtonPressed().Should().Be(GamepadButton.Start);
            input.BeginFrame();
            Engine3D.GetGamepadButtonPressed().Should().Be(GamepadButton.Start, "it is the last pressed while it is held, as raylib's is");
            first.SetButton(GamepadButton.South, true);
            Engine3D.GetGamepadButtonPressed().Should().Be(GamepadButton.South, "the last pressed on any pad");
            first.SetButton(GamepadButton.South, false);
            Engine3D.GetGamepadButtonPressed().Should().BeNull("once the last one is up, as raylib clears it, though Start is held");
            second.SetButton(GamepadButton.Start, false);

            input.AddWheel(2, -1);
            Engine3D.GetMouseWheelMoveV().Should().Be(new System.Numerics.Vector2(2, -1));
            Engine3D.GetMouseWheelMove().Should().Be(-1);
            Engine3D.SetMouseCursor(MouseCursor.PointingHand);

            // A letterboxed texture of half the window's size, 40 pixels in from its left edge.
            input.SetMousePosition(140, 100);
            Engine3D.SetMouseOffset(-40, 0);
            Engine3D.SetMouseScale(0.5f, 0.5f);
            Engine3D.GetMousePosition().Should().Be(new System.Numerics.Vector2(50, 50));
            (Engine3D.GetMouseX(), Engine3D.GetMouseY()).Should().Be((50, 50));
            Engine3D.EnableEventWaiting();
            Engine3D.DisableEventWaiting();

            var outline = new App();
            outline.World.InitResource<Input>();
            outline.World.InitResource<DrawList>();
            Engine3D.UseApp(outline);
            Engine3D.GetMousePosition().Should().Be(System.Numerics.Vector2.Zero, "a new app starts with no offset or scale");
            Engine3D.DrawRectangleRoundedLinesEx(new Rectangle(10, 10, 100, 50), 0.5f, 4, 6, Color.Red);
            var points = outline.World.Resource<DrawList>().Vertices.ToArray().Select(v => v.Position).ToArray();
            points.Should().NotBeEmpty();
            points.Should().OnlyContain(p => p.X >= 4 - 1e-3 && p.X <= 116 + 1e-3 && p.Y >= 4 - 1e-3 && p.Y <= 66 + 1e-3,
                "the band lies between the edge and six pixels outside it");
        }
        finally
        {
            Engine3D.UseApp(null);
        }
    }

    [Fact]
    public void Pads_Are_Indexed_In_The_Order_They_Connected()
    {
        var input = new Input();
        input.ConnectGamepad(10, "First", 0);
        input.ConnectGamepad(20, "Second", 0);
        input.DisconnectGamepad(10);

        input.Gamepad(0)!.Name.Should().Be("Second");
        input.Gamepad(1).Should().BeNull();
    }

    [Fact]
    public void The_Console_Makes_A_Pad_When_None_Is_Connected()
    {
        var input = new Input();

        SyntheticInput.Pad(input, 0)!.Id.Should().Be(SyntheticInput.ConsolePadId);
        SyntheticInput.Pad(input, 1).Should().BeNull();
        input.Gamepads.Should().ContainSingle();
    }

    [Fact]
    public void An_Axis_Reads_Back_What_Was_Set()
    {
        var pad = new Input().ConnectGamepad(1, "Pad", 0);

        pad.SetAxis(GamepadAxis.RightTrigger, 0.5f);

        pad.Axis(GamepadAxis.RightTrigger).Should().Be(0.5f);
        pad.Axis(GamepadAxis.LeftX).Should().Be(0f);
    }

    [Fact]
    public void A_Pads_Motion_And_Touchpad_Fingers_Are_Read_Through_The_Flat_Api()
    {
        var app = new App();
        var input = new Input();
        app.World.InsertResource(input);
        Engine3D.UseApp(app);
        try
        {
            var pad = input.ConnectGamepad(3, "Pad", 0);
            Engine3D.IsGamepadMotionAvailable(0).Should().BeFalse("a pad that has not said otherwise has no sensors");
            pad.HasMotion = true;
            pad.Gyro = new System.Numerics.Vector3(0, 1.5f, 0);
            pad.Accelerometer = new System.Numerics.Vector3(0, 9.81f, 0);
            pad.SetTouch(1, new System.Numerics.Vector2(0.75f, 0.5f));
            pad.SetTouch(0, new System.Numerics.Vector2(0.25f, 0.5f));

            Engine3D.IsGamepadMotionAvailable(0).Should().BeTrue();
            Engine3D.GetGamepadGyro(0).Y.Should().Be(1.5f);
            Engine3D.GetGamepadAccelerometer(0).Y.Should().Be(9.81f);
            Engine3D.GetGamepadTouchCount(0).Should().Be(2);
            Engine3D.GetGamepadTouchPosition(0, 0).X.Should().Be(0.25f, "fingers are in the order of their numbers");

            pad.SetTouch(0, null);
            Engine3D.GetGamepadTouchCount(0).Should().Be(1, "a lifted finger is gone");
            Engine3D.GetGamepadTouchPosition(0, 0).X.Should().Be(0.75f);
            Engine3D.GetGamepadGyro(1).Should().Be(System.Numerics.Vector3.Zero, "a pad that is not connected reads zero");
            Engine3D.SetGamepadLight(0, Color.Red);
        }
        finally
        {
            Engine3D.UseApp(null);
        }
    }
}
