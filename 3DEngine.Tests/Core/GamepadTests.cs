using FluentAssertions;

namespace Engine.Tests.Core;

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
}
