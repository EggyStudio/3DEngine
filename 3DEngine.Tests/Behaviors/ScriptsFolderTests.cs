using FluentAssertions;

namespace Engine.Tests.Behaviors;

/// <summary>Which folder of scripts a program watches, beside it or in the project it was built from.</summary>
[Trait("Category", "Unit")]
public sealed class ScriptsFolderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-scripts-");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void A_Program_Built_From_A_Project_With_Scripts_Watches_The_Projects_Own()
    {
        var project = Directory.CreateDirectory(Path.Combine(_folder.Path, "Game")).FullName;
        File.WriteAllText(Path.Combine(project, "Game.csproj"), "<Project />");
        var scripts = Directory.CreateDirectory(Path.Combine(project, "source", "behaviors")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(project, "bin", "Debug", "net10.0")).FullName;

        BehaviorsPlugin.DefaultScriptsDirectory(output).Should().Be(scripts, "a script saved where it is written takes hold at once");
    }

    [Fact]
    public void A_Program_Anywhere_Else_Watches_The_Scripts_Beside_It()
    {
        var shipped = Directory.CreateDirectory(Path.Combine(_folder.Path, "a", "b", "c", "Game")).FullName;

        BehaviorsPlugin.DefaultScriptsDirectory(shipped).Should().Be(Path.Combine(shipped, "source", "behaviors"));
    }
}
