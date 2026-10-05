using FluentAssertions;

namespace Engine.Tests.Gui;

/// <summary>Dear ImGui's one context for the process, held by one app at a time.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public class ImGuiHolderTests
{
    private static App Build() => new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());

    [Fact]
    [ExpectsError("Engine.Application", "Dear ImGui has one context")]
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

    [Fact]
    public void A_Focused_Text_Field_Says_Where_The_Input_Method_Composes_And_Leaving_It_Clears_That()
    {
        var app = Build();
        try
        {
            SdlImGuiIme.Install(0);
            var text = "";
            void Frame(bool field, bool focus)
            {
                app.BeginFrame();
                ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(100, 50));
                ImGuiNET.ImGui.Begin("ime");
                if (focus) ImGuiNET.ImGui.SetKeyboardFocusHere();
                if (field) ImGuiNET.ImGui.InputText("name", ref text, 64);
                ImGuiNET.ImGui.End();
                app.EndFrame();
            }

            Frame(field: true, focus: true);
            Frame(field: true, focus: false);
            Frame(field: true, focus: false);
            SdlImGuiIme.LastArea.Should().NotBeNull("a focused field has the input method placed beside it");
            var (position, lineHeight) = SdlImGuiIme.LastArea!.Value;
            position.X.Should().BeInRange(100, 300, "the cursor is in the field, in the window placed at 100, 50");
            position.Y.Should().BeInRange(50, 120);
            lineHeight.Should().BePositive();

            Frame(field: false, focus: false);
            Frame(field: false, focus: false);
            SdlImGuiIme.LastArea.Should().BeNull("with no field typed into, the area is cleared");
        }
        finally
        {
            app.Shutdown();
        }
    }

    [Fact]
    public void The_Frame_Profile_Is_Drawn_In_A_Window_Of_Its_Own_Grouped_As_The_Report_Has_It()
    {
        var app = Build();
        try
        {
            Engine3D.UseApp(app);
            for (int i = 0; i < 3; i++)
            {
                app.BeginFrame();
                Engine3D.DrawProfileWindow();
                app.EndFrame();
            }

            var (_, groups) = app.World.Resource<FrameProfile>().Snapshot();
            groups[0].Group.Should().Be("frame", "the frame comes first");
            var stages = groups.Single(g => g.Group == "stage").Averages;
            stages.Select(a => a.Milliseconds).Should().BeInDescendingOrder("largest first within a group");
        }
        finally
        {
            Engine3D.UseApp(null);
            app.Shutdown();
        }
    }
}
