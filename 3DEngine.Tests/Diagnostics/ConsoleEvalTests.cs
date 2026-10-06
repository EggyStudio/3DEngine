using FluentAssertions;

namespace Engine.Tests.Diagnostics;

/// <summary>C# typed at a running program through the console's <c>eval</c>.</summary>
[Collection("Console")]
[Trait("Category", "Integration")]
public sealed class ConsoleEvalTests
{
    private static App Setup()
    {
        var app = new App();
        var ecs = new EcsWorld();
        app.World.InsertResource(ecs);
        app.World.InsertResource(new Time());
        ecs.Add(ecs.Spawn(), new Transform(new System.Numerics.Vector3(0, 2, 0)));
        return app;
    }

    // The answer and the failure, if any, as the command line would be given them.
    private static (string Answer, string? Failure) Eval(App app, string code)
    {
        using (ConsoleHost.Lend(app.World, app))
        {
            var answer = ConsoleCommands.Run("eval " + code) ?? "";
            return (answer, ConsoleHost.Failure?.Code);
        }
    }

    [Fact]
    public void An_Expression_Answers_With_Its_Value_And_Statements_Change_The_World()
    {
        var app = Setup();

        Eval(app, "ecs.EntityCount").Should().Be(("1", null));
        Eval(app, "var e = ecs.Spawn(); ecs.Add(e, new Transform(Vector3.One)); ecs.EntityCount").Answer.Should().Be("2");
        Eval(app, "var count = ecs.EntityCount; count").Answer.Should().Be("2", "a name alone is an expression once it is ended");
        app.World.Resource<EcsWorld>().EntityCount.Should().Be(2, "the fragment ran against the program's own world");
        Eval(app, "world.TryGetResource<Time>(out _);").Answer.Should().Be("null", "a fragment ending in a statement answers nothing");
        Eval(app, "new Transform(Vector3.One)").Answer.Should().StartWith("{ Position=<1, 1, 1>", "a value with no text of its own is shown by its fields");
    }

    [Fact]
    public void A_File_May_Take_Usings_And_Declare_Functions_And_Types_After_Its_Statements()
    {
        var app = Setup();

        var (answer, failure) = Eval(app, """
            using System.Text;

            var heights = new StringBuilder();
            foreach (var (_, transform) in ecs.Query<Transform>())
                heights.Append(transform.Position.Y);
            await Task.Yield();
            Shout(new Note(heights.ToString()))

            static string Shout(Note note) => note.Text + "!";

            record Note(string Text);
            """);

        failure.Should().BeNull();
        answer.Should().Be("2!", "the bare expression before the local function is the last statement");
    }

    [Fact]
    public void Code_That_Does_Not_Compile_Is_Refused_With_Its_Place()
    {
        var app = Setup();

        var (answer, failure) = Eval(app, "ecs.Nothing");
        (answer, failure).Should().Be(("not compiled", "EVAL_COMPILE_FAILED"));
        using (ConsoleHost.Lend(app.World, app))
        {
            ConsoleCommands.Run("eval ecs.Nothing");
            ConsoleHost.Failure!.Value.Message.Should().StartWith("1:5 CS1061");
        }
    }

    [Fact]
    [ExpectsError("Engine.Console", "An eval fragment threw")]
    public void An_Exception_Is_Answered_With_Its_Type_And_Message()
    {
        var app = Setup();

        using (ConsoleHost.Lend(app.World, app))
        {
            ConsoleCommands.Run("eval throw new InvalidOperationException(\"on purpose\")").Should().Be("threw");
            ConsoleHost.Failure.Should().Be(new CliError("EVAL_THREW", "InvalidOperationException: on purpose"));
        }
    }
}
