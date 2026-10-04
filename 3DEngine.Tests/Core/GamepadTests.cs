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
    public void Any_Pad_Pressing_A_Button_Answers_Press_Any_Button_And_The_Wheel_Reads_Both_Axes()
    {
        var app = new App();
        app.World.InitResource<Input>();
        Engine3D.UseApp(app);
        try
        {
            var input = app.World.Resource<Input>();
            input.ConnectGamepad(1, "First", 0);
            var second = input.ConnectGamepad(2, "Second", 0);

            Engine3D.GetGamepadButtonPressed().Should().BeNull();
            second.SetButton(GamepadButton.Start, true);
            Engine3D.GetGamepadButtonPressed().Should().Be(GamepadButton.Start);
            input.BeginFrame();
            Engine3D.GetGamepadButtonPressed().Should().BeNull("only the frame it went down");

            input.AddWheel(2, -1);
            Engine3D.GetMouseWheelMoveV().Should().Be(new System.Numerics.Vector2(2, -1));
            Engine3D.GetMouseWheelMove().Should().Be(-1);
            Engine3D.SetMouseCursor(MouseCursor.PointingHand);
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
