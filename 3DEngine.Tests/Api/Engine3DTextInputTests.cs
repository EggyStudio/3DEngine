using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Where the input method's window goes for a text box a program draws itself, in an app with no window.</summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DTextInputTests : IDisposable
{
    public Engine3DTextInputTests() => UseApp(new App());

    public void Dispose() => UseApp(null);

    [Fact]
    public void A_Text_Box_Sets_The_Input_Methods_Area_And_An_Empty_One_Clears_It()
    {
        SetTextInputArea(new Rectangle(200, 180, 400, 50), 12);
        TextInputArea.Should().Be((new Rectangle(200, 180, 400, 50), 12));

        SetTextInputArea(default);
        TextInputArea.Should().BeNull("an empty area lets the platform place the window");
    }
}
