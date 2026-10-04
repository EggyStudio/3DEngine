using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Touch through the flat API, in an app with no window, fed as the platform feeds it.</summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DTouchTests : IDisposable
{
    private readonly App _app = new();

    public Engine3DTouchTests()
    {
        _app.World.InsertResource(new Input());
        UseApp(_app);
    }

    public void Dispose() => UseApp(null);

    private Input Input => _app.World.Resource<Input>();

    [Fact]
    public void Fingers_Are_Listed_In_The_Order_They_Touched_And_Leave_When_Lifted()
    {
        Input.SetTouch(7, 10, 20, down: true);
        Input.SetTouch(3, 30, 40, down: true);
        Input.SetTouch(7, 12, 22, down: true);

        GetTouchPointCount().Should().Be(2);
        (GetTouchPointId(0), GetTouchPosition(0)).Should().Be((7, new Vector2(12, 22)), "a moved finger keeps its place in the list");
        (GetTouchPointId(1), GetTouchPosition(1)).Should().Be((3, new Vector2(30, 40)));
        (GetTouchX(), GetTouchY()).Should().Be((12, 22));

        Input.SetTouch(7, 0, 0, down: false);
        GetTouchPointCount().Should().Be(1);
        GetTouchPointId(0).Should().Be(3, "the finger left is first");
        GetTouchPointId(1).Should().Be(-1);
    }

    [Fact]
    public void With_No_Finger_Down_The_Left_Mouse_Button_Stands_In_For_One()
    {
        Input.SetMousePosition(50, 60);
        GetTouchPointCount().Should().Be(0);
        GetTouchPosition(0).Should().Be(new Vector2(50, 60), "the first touch is the pointer, as raylib has it");

        Input.SetMouseButton(MouseButton.Left, true);
        GetTouchPointCount().Should().Be(1, "a held left button counts as a finger");
        GetTouchPosition(1).Should().Be(Vector2.Zero);
    }
}
