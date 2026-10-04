using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class Engine3DStateTests : IDisposable
{
    private enum Screen { Menu, Play, Pause }
    private enum Menu { Main, Options }

    public Engine3DStateTests() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 240 }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    public void A_State_Moves_At_The_Next_Frame()
    {
        AddState(Screen.Menu);
        BeginDrawing();
        EndDrawing();
        GetState<Screen>().Should().Be(Screen.Menu);

        SetState(Screen.Play);
        IsState(Screen.Menu).Should().BeTrue("the move waits for the next frame");
        BeginDrawing();
        EndDrawing();

        IsState(Screen.Play).Should().BeTrue();
    }

    [Fact]
    public void A_State_Never_Added_Is_Reported()
    {
        var get = () => GetState<Screen>();
        get.Should().Throw<InvalidOperationException>().WithMessage("*AddState*");
    }

    [Fact]
    public void A_Sub_State_Is_Not_At_Any_Value_While_Its_Parent_Is_Elsewhere()
    {
        AddState(Screen.Play);
        AddSubState(Screen.Menu, Menu.Main);
        BeginDrawing();
        EndDrawing();
        IsState(Menu.Main).Should().BeFalse("the menu's sub-state does not exist during play");

        SetState(Screen.Menu);
        BeginDrawing();
        EndDrawing();
        IsState(Menu.Main).Should().BeTrue();
    }
}
