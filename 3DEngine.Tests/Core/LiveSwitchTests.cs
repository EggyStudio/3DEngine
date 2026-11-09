using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>
/// Whether a program is live, from where it runs, its own project's build folder or anywhere else,
/// and from the command line, the environment and its config, which say otherwise.
/// </summary>
/// <remarks>
/// In the E3D collection with the other test that replaces the run's arguments, which an app made
/// beside it would read.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class LiveSwitchTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-live-switch-test-");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void A_Program_Runs_From_Its_Own_Project_Where_A_Project_File_There_Builds_It()
    {
        var project = _folder.Path;
        var program = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(program);
        File.WriteAllText(Path.Combine(project, "Slide.csproj"), "<Project />");

        ProgramFiles.OwnProject(program, "Slide").Should().Be(Path.GetFullPath(project), "the project file is named for the program");
        ProgramFiles.OwnProject(program, "testhost").Should().BeNull("a test host is not the project's program");
        ProgramFiles.OwnProject(program, null).Should().BeNull();
        ProgramFiles.OwnProject(project, "Slide").Should().BeNull("a program run from anywhere else, as a shipped one is, has no project");

        File.WriteAllText(Path.Combine(project, "Slide.csproj"), "<Project><PropertyGroup><AssemblyName>Coins</AssemblyName></PropertyGroup></Project>");
        ProgramFiles.OwnProject(program, "Coins").Should().Be(Path.GetFullPath(project), "or the assembly name it sets is the program's");
    }

    [Fact]
    public void A_Live_Program_Watches_The_Asset_Servers_Files_Too()
    {
        foreach (var live in new[] { true, false })
        {
            using var app = new App(Config.Default with { Live = live, Headless = true });
            app.AddPlugin(new AssetPlugin());
            app.World.Resource<AssetServer>().WatchForChanges.Should().Be(live, "the switch turns the asset server's watch on with the flat API's");
        }
    }

    [Fact]
    public void The_Command_Line_And_The_Environment_Say_Whether_A_Program_Is_Live_Either_Way()
    {
        var (arguments, variable) = (RunMode.Arguments, RunMode.Variable);
        try
        {
            RunMode.Variable = _ => null;
            RunMode.Arguments = () => ["game"];
            RunMode.Apply(Config.Default).Live.Should().BeNull("where nothing says, where the program runs decides");
            RunMode.Apply(Config.Default with { Live = false }).Live.Should().BeFalse("the program's own config says it");
            RunMode.IsLive(Config.Default with { Live = true }).Should().BeTrue();
            RunMode.IsLive(Config.Default).Should().BeFalse("the test host runs from no project of its own");

            RunMode.Arguments = () => ["game", "--live"];
            RunMode.Apply(Config.Default with { Live = false }).Live.Should().BeTrue("the command line says it over the config");
            RunMode.Arguments = () => ["game", "--no-live"];
            RunMode.Apply(Config.Default with { Live = true }).Live.Should().BeFalse();

            RunMode.Arguments = () => ["game"];
            RunMode.Variable = name => name == "E3D_LIVE" ? "off" : null;
            RunMode.Apply(Config.Default with { Live = true }).Live.Should().BeFalse();
            RunMode.Variable = name => name == "E3D_LIVE" ? "1" : null;
            RunMode.Apply(Config.Default).Live.Should().BeTrue();
        }
        finally
        {
            (RunMode.Arguments, RunMode.Variable) = (arguments, variable);
        }
    }
}
