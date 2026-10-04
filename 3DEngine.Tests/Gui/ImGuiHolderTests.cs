using FluentAssertions;

namespace Engine.Tests.Gui;

/// <summary>Dear ImGui's one context for the process, held by one app at a time.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public class ImGuiHolderTests
{
    private static App Build() => new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());

    [Fact]
    public void A_Second_App_Using_ImGui_While_The_First_Is_Alive_Is_Refused_Plainly()
    {
        var first = Build();
        try
        {
            var second = () => Build();
            second.Should().Throw<InvalidOperationException>().WithMessage("*one context for the whole process*");

            // The first keeps drawing with it.
            first.BeginFrame();
            ImGuiNET.ImGui.Begin("still mine");
            ImGuiNET.ImGui.End();
            first.EndFrame();
        }
        finally
        {
            first.Shutdown();
        }
    }

    [Fact]
    public void An_App_Built_After_The_First_Shut_Down_Uses_ImGui()
    {
        Build().Shutdown();

        var next = Build();
        next.BeginFrame();
        ImGuiNET.ImGui.Begin("after");
        ImGuiNET.ImGui.End();
        next.EndFrame();
        next.Shutdown();
    }
}
