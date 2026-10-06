using FluentAssertions;
using ImGuiNET;

namespace Engine.Tests.Gui;

/// <summary>A gamepad driving ImGui's navigation, so a game's menus work with the pad alone.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public class ImGuiGamepadTests
{
    // A menu of three buttons in a window that takes the focus, the items picked collected.
    private sealed class Menu : IDisposable
    {
        public readonly App App = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());
        public readonly List<string> Picked = [];

        public void Frame()
        {
            App.BeginFrame();
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(20, 20));
            ImGui.SetNextWindowFocus();
            ImGui.Begin("menu");
            foreach (var item in new[] { "Play", "Settings", "Quit" })
                if (ImGui.Button(item)) Picked.Add(item);
            ImGui.End();
            App.EndFrame();
        }

        public void Press(GamepadState pad, GamepadButton button)
        {
            pad.SetButton(button, true);
            Frame();
            pad.SetButton(button, false);
            Frame();
        }

        public void Dispose() => App.Shutdown();
    }

    [Fact]
    public void The_Pads_Dpad_Moves_Through_A_Menu_And_Its_Bottom_Button_Picks_An_Item()
    {
        using var menu = new Menu();
        menu.Frame();
        ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.HasGamepad).Should().BeFalse("no pad is connected yet");

        var pad = menu.App.World.Resource<Input>().ConnectGamepad(1, "test pad", 0);
        menu.Frame();
        ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.HasGamepad).Should().BeTrue();

        // Navigation starts on the focused window's first item, so down twice is the third.
        menu.Press(pad, GamepadButton.LeftFaceDown);
        menu.Press(pad, GamepadButton.LeftFaceDown);
        menu.Press(pad, GamepadButton.RightFaceDown);
        menu.Picked.Should().Equal(["Quit"], "the pad moved down two items and picked that one");

        // Up one and picked again.
        menu.Press(pad, GamepadButton.LeftFaceUp);
        menu.Press(pad, GamepadButton.RightFaceDown);
        menu.Picked.Should().Equal(["Quit", "Settings"]);
    }

    [Fact]
    public void The_First_Press_Of_The_Pad_On_A_Menu_Picks_Its_First_Item()
    {
        using var menu = new Menu();
        var pad = menu.App.World.Resource<Input>().ConnectGamepad(1, "test pad", 0);
        menu.Frame();
        menu.Frame();

        menu.Press(pad, GamepadButton.RightFaceDown);
        menu.Picked.Should().Equal(["Play"], "the press picks, where ImGui alone only shows its cursor on the first");
    }
}
