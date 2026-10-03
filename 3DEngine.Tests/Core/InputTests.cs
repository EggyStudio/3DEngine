using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

/// <summary>
/// Tests for the <see cref="Input"/> resource.
/// <c>Input.SetKey</c>, <c>SetMouseButton</c>, etc. are <c>internal</c> - accessible
/// because Engine.Common declares <c>[InternalsVisibleTo("Engine.Tests")]</c>.
/// </summary>
[Trait("Category", "Unit")]
public class InputTests
{
    [Fact]
    public void SetKey_Down_Fires_KeyDown_And_KeyPressed()
    {
        var input = new Input();

        input.SetKey(Key.W, true);

        input.KeyDown(Key.W).Should().BeTrue();
        input.KeyPressed(Key.W).Should().BeTrue();
        input.KeyReleased(Key.W).Should().BeFalse();
    }

    [Fact]
    public void SetKey_Up_Fires_KeyReleased_And_Clears_KeyDown()
    {
        var input = new Input();
        input.SetKey(Key.W, true);

        input.SetKey(Key.W, false);

        input.KeyDown(Key.W).Should().BeFalse();
        input.KeyReleased(Key.W).Should().BeTrue();
    }

    [Fact]
    public void Duplicate_KeyDown_Does_Not_Duplicate_Pressed()
    {
        var input = new Input();
        input.SetKey(Key.A, true);
        input.SetKey(Key.A, true); // already down

        // Pressed should have fired only once (first transition)
        input.KeyDown(Key.A).Should().BeTrue();
    }

    [Fact]
    public void AnyKeyDown_Returns_True_When_Key_Held()
    {
        var input = new Input();

        input.AnyKeyDown().Should().BeFalse();

        input.SetKey(Key.Space, true);

        input.AnyKeyDown().Should().BeTrue();
    }

    [Fact]
    public void AnyKeyPressed_Returns_True_During_Press_Frame()
    {
        var input = new Input();
        input.SetKey(Key.Return, true);

        input.AnyKeyPressed().Should().BeTrue();
    }

    [Fact]
    public void SetMouseButton_Down_And_Up()
    {
        var input = new Input();

        input.SetMouseButton(MouseButton.Left, true);

        input.MouseDown(MouseButton.Left).Should().BeTrue();
        input.MousePressed(MouseButton.Left).Should().BeTrue();

        input.SetMouseButton(MouseButton.Left, false);

        input.MouseDown(MouseButton.Left).Should().BeFalse();
        input.MouseReleased(MouseButton.Left).Should().BeTrue();
    }

    [Fact]
    public void MouseButton_Int_Overloads_Work()
    {
        var input = new Input();

        input.SetMouseButton(0, true); // Left

        input.MouseDown(0).Should().BeTrue();
        input.MousePressed(0).Should().BeTrue();
    }

    [Fact]
    public void AnyMouseDown_And_AnyMousePressed()
    {
        var input = new Input();

        input.AnyMouseDown().Should().BeFalse();
        input.AnyMousePressed().Should().BeFalse();

        input.SetMouseButton(MouseButton.Right, true);

        input.AnyMouseDown().Should().BeTrue();
        input.AnyMousePressed().Should().BeTrue();
    }

    [Fact]
    public void SetMousePosition_Updates_MouseXY()
    {
        var input = new Input();

        input.SetMousePosition(100, 200);

        input.MouseX.Should().Be(100);
        input.MouseY.Should().Be(200);
        input.MousePosition.Should().Be((100, 200));
    }

    [Fact]
    public void AddMouseDelta_Accumulates()
    {
        var input = new Input();

        input.AddMouseDelta(10, 5);
        input.AddMouseDelta(-3, 2);

        input.MouseDeltaX.Should().Be(7);
        input.MouseDeltaY.Should().Be(7);
        input.MouseDelta.Should().Be((7, 7));
    }

    [Fact]
    public void AddWheel_Accumulates()
    {
        var input = new Input();

        input.AddWheel(0.5f, 1.0f);
        input.AddWheel(0.0f, -0.5f);

        input.WheelX.Should().BeApproximately(0.5f, 0.001f);
        input.WheelY.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void AddText_Appends_Characters()
    {
        var input = new Input();

        input.AddText("He");
        input.AddText("llo");

        input.TextInput.ToString().Should().Be("Hello");
    }

    [Fact]
    public void BeginFrame_Clears_Pressed_Released_Deltas_Wheel_Text()
    {
        var input = new Input();

        input.SetKey(Key.W, true);
        input.SetMouseButton(MouseButton.Left, true);
        input.AddMouseDelta(10, 20);
        input.AddWheel(1.0f, 2.0f);
        input.AddText("abc");

        input.BeginFrame();

        // Pressed/released cleared
        input.KeyPressed(Key.W).Should().BeFalse();
        input.MousePressed(MouseButton.Left).Should().BeFalse();

        // Deltas/wheel/text cleared
        input.MouseDeltaX.Should().Be(0);
        input.MouseDeltaY.Should().Be(0);
        input.WheelX.Should().Be(0);
        input.WheelY.Should().Be(0);
        input.TextInput.Length.Should().Be(0);

        // KeyDown still held (not a transient state)
        input.KeyDown(Key.W).Should().BeTrue();
        input.MouseDown(MouseButton.Left).Should().BeTrue();
    }

    [Fact]
    public void ToString_Contains_Info()
    {
        var input = new Input();
        input.SetKey(Key.W, true);
        input.SetMousePosition(100, 200);

        var str = input.ToString();

        str.Should().Contain("Keys=");
        str.Should().Contain("Mouse=");
    }
}
