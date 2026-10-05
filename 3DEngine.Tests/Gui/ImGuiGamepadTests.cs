using FluentAssertions;
using ImGuiNET;

namespace Engine.Tests.Gui;

/// <summary>A gamepad driving ImGui's navigation, so a game's menus work with the pad alone.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public class ImGuiGamepadTests
{
    [Fact]
    public void The_Pads_Dpad_Moves_Through_A_Menu_And_Its_Bottom_Button_Picks_An_Item()
    {
        var app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());
        try
        {
            var input = app.World.Resource<Input>();
            var picked = new List<string>();
            void Frame()
            {
                app.BeginFrame();
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(20, 20));
                ImGui.SetNextWindowFocus();
                ImGui.Begin("menu");
                foreach (var item in new[] { "Play", "Settings", "Quit" })
                    if (ImGui.Button(item)) picked.Add(item);
                ImGui.End();
                app.EndFrame();
            }
            void Press(GamepadState pad, GamepadButton button)
            {
                pad.SetButton(button, true);
                Frame();
                pad.SetButton(button, false);
                Frame();
            }

            Frame();
            ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.HasGamepad).Should().BeFalse("no pad is connected yet");

            var pad = input.ConnectGamepad(1, "test pad", 0);
            Frame();
            ImGui.GetIO().BackendFlags.HasFlag(ImGuiBackendFlags.HasGamepad).Should().BeTrue();

            // Navigation starts on the focused window's first item, so down twice is the third.
            Press(pad, GamepadButton.DpadDown);
            Press(pad, GamepadButton.DpadDown);
            Press(pad, GamepadButton.South);
            picked.Should().Equal(["Quit"], "the pad moved down two items and picked that one");

            // Up one and picked again.
            Press(pad, GamepadButton.DpadUp);
            Press(pad, GamepadButton.South);
            picked.Should().Equal(["Quit", "Settings"]);
        }
        finally
        {
            app.Shutdown();
        }
    }
}
