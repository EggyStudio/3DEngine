using System.Numerics;
using FluentAssertions;
using ImGuiNET;
using SDL3;

namespace Engine.Tests.Gui;

/// <summary>
/// With ImGui's viewports on, an ImGui window placed outside the main window is given an SDL window
/// and a swapchain of its own and drawn into it, and both are closed when it comes back inside.
/// </summary>
/// <remarks>
/// The app's window is opened on SDL's offscreen video driver, which keeps where its windows are and
/// needs no display. The test runs alone, since the app's closing quits SDL for the process, which
/// would stop another test's audio device.
/// </remarks>
[Collection("Window")]
[Trait("Category", "Render")]
public sealed class ImGuiViewportTests
{
    [NeedsHeadlessWindowFact]
    public void A_Window_Placed_Outside_The_Main_One_Gets_A_Window_And_A_Swapchain_Closed_When_It_Comes_Back()
    {
        var errorsBefore = GraphicsDevice.ValidationErrors.Count;
        SDL.SetHint(SDL.Hints.VideoDriver, "offscreen");
        var app = new App(Config.Default.WithWindow("viewports test", 320, 240) with { Hidden = true }).AddPlugin(new DefaultPlugins());
        try
        {
            SdlImGuiViewports.Installed.Should().BeTrue("SDL's offscreen driver keeps where its windows are, so viewports are offered");
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

            // The window placed from the main window's corner, which ImGui measures from the desktop.
            void Frames(Vector2 at, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    app.BeginFrame();
                    ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + at, ImGuiCond.Always);
                    ImGui.SetNextWindowSize(new Vector2(160, 80), ImGuiCond.Always);
                    ImGui.Begin("Outside");
                    ImGui.Text("In a window of its own");
                    ImGui.End();
                    app.EndFrame();
                }
            }

            Frames(new Vector2(400, 40), 4);
            var viewports = ImGui.GetPlatformIO().Viewports;
            viewports.Size.Should().Be(2, "the window wholly outside the main one has a viewport of its own");
            var window = viewports[1].PlatformHandle;
            window.Should().NotBe(0, "the viewport has an SDL window");
            var id = SDL.GetWindowID(window);
            SDL.GetWindowSize(window, out var width, out var height);
            (width, height).Should().Be((160, 80));
            var surface = SdlImGuiViewports.Made(viewports[1]);
            surface.Should().NotBeNull("the window has a swapchain");
            surface!.Size.Should().Be(new Extent2D(160, 80));

            byte[]? pixels = null;
            GraphicsDevice.RequestWindowCapture(surface, (rgba, _, _) => pixels = rgba);
            Frames(new Vector2(400, 40), 2);
            pixels.Should().NotBeNull("the window's frame is presented with the main one's");
            Enumerable.Range(0, pixels!.Length / 4).Max(i => pixels[i * 4]).Should().BeGreaterThan(200,
                "ImGui's window is drawn into it, its text white on the dark ground");

            Frames(new Vector2(20, 20), 4);
            ImGui.GetPlatformIO().Viewports.Size.Should().Be(1, "back inside, the window is drawn in the main one");
            SDL.GetWindowFromID(id).Should().Be(0, "and its own window is closed");
            GraphicsDevice.ValidationErrors.Skip(errorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        }
        finally
        {
            app.Shutdown();
            SDL.ResetHint(SDL.Hints.VideoDriver);
        }
    }
}

/// <summary>Tests that open a window of SDL's, run alone, since closing it quits SDL for the whole process.</summary>
[CollectionDefinition("Window", DisableParallelization = true)]
public sealed class WindowCollection;
