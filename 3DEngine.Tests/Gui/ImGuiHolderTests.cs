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
    public void A_Window_Made_In_First_Is_Drawn_In_The_Frame_A_System_In_Update_Draws_In()
    {
        // ImGui's frame begins in First, after the frame's time and the commands served there and
        // before a program's own systems in it, so a window one of them makes is drawn.
        var app = Build();
        try
        {
            int? first = null, update = null;
            var size = System.Numerics.Vector2.Zero;
            app.AddSystem(Stage.First, new SystemDescriptor(_ =>
            {
                ImGuiNET.ImGui.Begin("made in First");
                ImGuiNET.ImGui.Text("drawn from First");
                size = ImGuiNET.ImGui.GetWindowSize();
                first = ImGuiNET.ImGui.GetFrameCount();
                ImGuiNET.ImGui.End();
            }, "Test.First").MainThreadOnly());
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => update = ImGuiNET.ImGui.GetFrameCount(), "Test.Update").MainThreadOnly());

            for (int frame = 0; frame < 3; frame++)
            {
                app.BeginFrame();
                app.EndFrame();
            }
            first.Should().NotBeNull().And.Be(update, "the system in First draws in the frame the one in Update draws in");
            size.X.Should().BePositive("its window was made and sized");
        }
        finally
        {
            app.Shutdown();
        }
    }

    [Fact]
    public void A_Run_No_One_Sees_Leaves_No_Layout_Behind()
    {
        // ImGui writes its windows' places to imgui.ini in the working directory as it shuts down,
        // which a run that shows no window, as ./e3d's and the tests', neither reads nor writes, so
        // the next starts from the program's own layout.
        var ini = Path.Combine(Environment.CurrentDirectory, "imgui.ini");
        File.Delete(ini);
        var app = Build();
        app.BeginFrame();
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(120, 80));
        ImGuiNET.ImGui.Begin("placed");
        ImGuiNET.ImGui.End();
        app.EndFrame();
        app.Shutdown();

        File.Exists(ini).Should().BeFalse("a run no one sees keeps no layout for the next");
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
