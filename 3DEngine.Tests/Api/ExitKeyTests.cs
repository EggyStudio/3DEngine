using Diagnostics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// Escape handed down one order, the settings panel open taking it to go back a page with the
/// window left open, a game's own menu holding it, an example's window left on it where nothing
/// open takes it, and a game with no exit key set kept open, in a headless run.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class ExitKeyTests : IDisposable
{
    public ExitKeyTests() => UseApp(NewApp());

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static App NewApp() =>
        new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins());

    private static Input Input => GetApp().World.Resource<Input>();

    private static PanelModel Panel => DiagnosticsBinding.Of(GetApp().World).Panel;

    // Frames as a game's loop runs them, each asking WindowShouldClose first, which reads the
    // frame's events, answering what the last one answered.
    private static bool Frames(int count)
    {
        var close = false;
        for (var i = 0; i < count; i++)
        {
            close = WindowShouldClose();
            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            EndDrawing();
        }
        return close;
    }

    // A key held through a frame and let go in the next, answering whether the window was asked
    // to close.
    private static bool Tap(Key key)
    {
        Input.SetKey(key, true);
        Frames(1);
        Input.SetKey(key, false);
        return Frames(1);
    }

    // A key pressed as the e3d client presses it, reaching ImGui as well as the game, and let go
    // as the e3d server's loop lets it go, answering whether the window was asked to close.
    private static bool Press(Key key)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp())) ConsoleHost.Run($"input.key {key} 1");
        Frames(2);
        GetApp().World.Resource<SyntheticInput>().Update(Input, ulong.MaxValue);
        return Frames(2);
    }

    [Fact]
    public void Escape_With_The_Settings_Panel_Open_Goes_Back_A_Page_And_Leaves_The_Window_Open()
    {
        SetExitKey(Key.Escape);
        Frames(1);
        Tap(Key.F4);
        Frames(2);
        Panel.Open.Should().BeTrue("F4 opens the settings panel");
        for (var i = 0; i < 64 && Panel.Rows.Single(row => row.Selected).Kind != PanelRowKind.Page; i++) Panel.Press(PanelInput.Down);
        Panel.Press(PanelInput.Accept);
        Panel.Title.Should().NotBe(Panel.Root.Title, "a page of the panel is open");

        Press(Key.Escape).Should().BeFalse("the panel took Escape and the window did not see it");
        (Panel.Open, Panel.Title).Should().Be((true, Panel.Root.Title), "Escape goes back a page");
        IsExitKeyHeld().Should().BeTrue("the open panel holds the key");

        Press(Key.Escape).Should().BeFalse();
        Panel.Open.Should().BeFalse("Escape on the panel's first page goes out of it");
        IsExitKeyHeld().Should().BeFalse("nothing is open to hold the key");
    }

    [Fact]
    public void Escape_With_Nothing_Open_Leaves_An_Example_And_A_Games_Own_Menu_Holds_It_First()
    {
        File.ReadAllText(Path.Combine(CheatsheetTests.RepoRoot(), "3DEngine.Examples", "Program.cs"))
            .Should().Contain("SetExitKey(Engine.Key.Escape)", "the examples program sets Escape as each example's exit key");
        SetExitKey(Key.Escape);
        var menu = true;
        HoldExitKeyWhile(() => menu);
        Frames(1);
        Tap(Key.Escape).Should().BeFalse("the game's own menu holds the key");

        menu = false;
        Tap(Key.Escape).Should().BeTrue("with nothing open, Escape leaves the example");
    }

    [Fact]
    public void A_Game_With_No_Exit_Key_Set_Stays_Open_On_Escape_And_A_Key_Set_Goes_With_Its_Window()
    {
        Frames(1);
        Tap(Key.Escape).Should().BeFalse("the engine sets no exit key of its own");

        SetExitKey(Key.Escape);
        CloseWindow();
        UseApp(NewApp());
        Frames(1);
        Tap(Key.Escape).Should().BeFalse("the key set went with the window that set it");
    }
}
