using Diagnostics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.DiagnosticsHost;

/// <summary>
/// F4 and F5 opening the Diagnostics module's console and settings panel one at a time, a line
/// typed into the console's ImGui field run with the world lent and an answer that comes later
/// written under it when it comes, a line the console or the panel runs whose command throws
/// written to the log, and the keys' settings standing in the registry while the app runs,
/// rebound there, and taken out as it ends, in a headless run.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class DiagnosticWindowsTests : IDisposable
{
    public DiagnosticWindowsTests() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static DiagnosticsBinding Binding => DiagnosticsBinding.Of(GetApp().World);

    private static Input Input => GetApp().World.Resource<Input>();

    private static void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            EndDrawing();
        }
    }

    // A key held through a frame and let go in the next.
    private static void Tap(Key key)
    {
        Input.SetKey(key, true);
        Frames(1);
        Input.SetKey(key, false);
        Frames(1);
    }

    // A line run as the e3d client runs it, with the world lent, whose input reaches ImGui too.
    private static string Run(string line)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp())) return ConsoleHost.Run(line).Text;
    }

    [Fact]
    public void F4_Opens_The_Console_And_F5_The_Panel_And_Each_Closes_Them_Again()
    {
        Frames(1);
        Tap(Key.F4);
        Binding.Console.Open.Should().BeTrue("F4 opens the console");
        Tap(Key.F5);
        (Binding.Panel.Open, Binding.Console.Open).Should().Be((true, false), "F5 opens the settings panel, and one is open at a time");
        Binding.Keys.Panels.Open.Should().Be(OpenPanels.PanelName);
        Binding.Panel.Rows.Should().Contain(row => row.Kind == PanelRowKind.Page && row.Label == "keys", "the keys' settings are a page of the panel");
        Frames(2);

        Tap(Key.F5);
        Binding.Panel.Open.Should().BeFalse("F5 again closes what it opened");
        Tap(Key.F4);
        Tap(Key.F4);
        (Binding.Panel.Open, Binding.Console.Open).Should().Be((false, false), "and F4 again closes what it opened");
        Binding.Keys.Panels.Open.Should().BeNull();
    }

    [Fact]
    public void A_Line_Typed_Into_The_Console_Runs_With_The_World_Lent_And_A_Later_Answer_Is_Written_Under_It()
    {
        var asked = 0;
        ConsoleHost.Registry.Add(new Command("probe.later", "Answers at once and again a few frames later.", "probe.later", _ =>
        {
            ConsoleHost.Later(() => ++asked >= 3 ? "answered later" : null);
            return new Answer($"{ConsoleHost.Ecs.EntityCount} entities, answering later");
        }));
        try
        {
            Frames(1);
            Tap(Key.F4);
            // Drawn open, the console's field takes the keyboard, and the line is typed and entered
            // through ImGui's input.
            Frames(2);
            Run("input.text probe.later");
            Frames(2);
            Run("input.key Enter 1");
            Frames(8);

            var lines = Binding.Console.Lines().Select(line => line.Text).ToList();
            var entered = lines.IndexOf("> probe.later");
            entered.Should().BeGreaterThanOrEqualTo(0, $"the line was entered in the console, which holds {string.Join(" | ", lines)}");
            var answered = lines.FindIndex(line => line.EndsWith(" entities, answering later", StringComparison.Ordinal));
            answered.Should().BeGreaterThan(entered, "the command ran with the world lent and answered under the line");
            lines.IndexOf("answered later").Should().BeGreaterThan(answered, "the answer that came later is written once it came");
        }
        finally
        {
            ConsoleHost.Registry.Remove("probe.later");
        }
    }

    // The two errors the test expects are its check that each line is logged with what it threw.
    [Fact]
    [ExpectsError("Engine.Console", "thrown for 'console'")]
    [ExpectsError("Engine.Console", "thrown for 'panel'")]
    public void A_Line_The_Console_Or_The_Panel_Runs_Whose_Command_Throws_Is_Logged_With_What_It_Threw()
    {
        ConsoleHost.Registry.Add(new Command("probe.throw", "Throws, naming the words after it.", "probe.throw <words>",
            line => throw new InvalidOperationException($"thrown for '{line.Rest}'")));
        try
        {
            Frames(1);
            Binding.Console.Submit("probe.throw console").Error.Should().BeOfType<InvalidOperationException>();

            Binding.Panel.Root.AddAction("Throw", "probe.throw panel");
            for (var step = 0; step < 50 && Binding.Panel.Rows[Binding.Panel.Selected].Label != "Throw"; step++)
                Binding.Panel.Press(PanelInput.Down);
            Binding.Panel.Press(PanelInput.Accept);
            Binding.Panel.Answer!.Error.Should().BeOfType<InvalidOperationException>("the panel's action ran the line");
        }
        finally
        {
            ConsoleHost.Registry.Remove("probe.throw");
        }
    }

    [Fact]
    public void The_Keys_Settings_Stand_In_The_Registry_Are_Rebound_There_And_Are_Taken_Out_As_The_App_Ends()
    {
        Frames(1);
        var keys = Binding.Keys;
        ConsoleHost.Registry.FindSetting("keys.console").Should().BeSameAs(keys.KeyOf(KeyAction.Console));
        Run("keys.console F6").Should().Be("keys.console is F6.");

        Tap(Key.F4);
        Binding.Console.Open.Should().BeFalse("F4 is no longer the console's key");
        Tap(Key.F6);
        Binding.Console.Open.Should().BeTrue("the key the registry's setting names opens it");

        CloseWindow();
        UseApp(null);
        ConsoleHost.Registry.FindSetting("keys.console").Should().NotBeSameAs(keys.KeyOf(KeyAction.Console), "the app's keys are taken out as it ends");
    }
}
