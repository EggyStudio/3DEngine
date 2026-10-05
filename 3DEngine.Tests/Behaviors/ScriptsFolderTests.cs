using FluentAssertions;

namespace Engine.Tests.Behaviors;

/// <summary>Which folder of scripts a program watches, beside it or in the project it was built from.</summary>
[Trait("Category", "Unit")]
public sealed class ScriptsFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("engine-scripts-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_Program_Built_From_A_Project_With_Scripts_Watches_The_Projects_Own()
    {
        var project = Directory.CreateDirectory(Path.Combine(_root, "Game")).FullName;
        File.WriteAllText(Path.Combine(project, "Game.csproj"), "<Project />");
        var scripts = Directory.CreateDirectory(Path.Combine(project, "source", "behaviors")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(project, "bin", "Debug", "net10.0")).FullName;

        BehaviorsPlugin.DefaultScriptsDirectory(output).Should().Be(scripts, "a script saved where it is written takes hold at once");
    }

    [Fact]
    public void A_Program_Anywhere_Else_Watches_The_Scripts_Beside_It()
    {
        var shipped = Directory.CreateDirectory(Path.Combine(_root, "a", "b", "c", "Game")).FullName;

        BehaviorsPlugin.DefaultScriptsDirectory(shipped).Should().Be(Path.Combine(shipped, "source", "behaviors"));
    }
}
