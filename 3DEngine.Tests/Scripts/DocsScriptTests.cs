using System.Diagnostics;
using FluentAssertions;

namespace Engine.Tests.Scripts;

/// <summary>
/// <c>build/docs-on-package.py</c> builds every C# block of the guides against the packed package and
/// names a block that no longer builds by its page and line (REVIEW.md, item 4).
/// </summary>
[Trait("Category", "Integration")]
public sealed class DocsScriptTests : IDisposable
{
    private static readonly string Root = Api.CheatsheetTests.RepoRoot();
    private readonly TestFolder _folder = new("engine-docs-script-");

    public void Dispose() => _folder.Dispose();

    [NeedsPackageAndPythonFact]
    public void A_Stale_Block_Is_Named_By_Its_Page_And_Line_A_Good_One_Builds_And_A_Skipped_One_Is_Left_Out()
    {
        var page = _folder.File("guide.md");
        File.WriteAllText(page, string.Join("\n",
        [
            "# A guide",                                                   // 1
            "",                                                            // 2
            "<!-- compiled with:",                                         // 3
            "int width = 800;",                                            // 4
            "-->",                                                         // 5
            "```csharp",                                                   // 6
            "DrawText(\"Hello\", width / 2, 10, 20, Color.Black);",        // 7
            "```",                                                         // 8
            "",                                                            // 9
            "```csharp",                                                   // 10
            "// A call the surface does not have.",                        // 11
            "DrawTextSideways(\"Hello\", 10, 10);",                        // 12
            "```",                                                         // 13
            "",                                                            // 14
            "<!-- not compiled: a sketch, in another language -->",        // 15
            "```csharp",                                                   // 16
            "float4 main() : SV_Target { return 1; }",                     // 17
            "```",                                                         // 18
            "",
        ]));

        var start = TestScriptTests.Utf8(new ProcessStartInfo(Probes.Python.Value!) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true });
        start.ArgumentList.Add(Path.Combine(Root, "build", "docs-on-package.py"));
        start.ArgumentList.Add(page);
        start.Environment["GITHUB_ACTIONS"] = "true";
        using var python = Process.Start(start)!;
        var output = python.StandardOutput.ReadToEndAsync();
        var errors = python.StandardError.ReadToEndAsync();
        python.WaitForExit(300_000).Should().BeTrue();
        var log = output.Result.Replace("\r", "") + errors.Result;

        python.ExitCode.Should().NotBe(0, log);
        var lines = TestScriptTests.Lines(log);
        lines.Should().ContainSingle(l => l.EndsWith("guide.md:12: block 2: CS0103 The name 'DrawTextSideways' does not exist in the current context"), log);
        lines.Should().Contain(l => l.StartsWith("::error title=") && l.Contains("guide.md%2C block 2::CS0103"), "the error is an annotation, which a reader not signed in sees");
        lines.Should().NotContain(l => l.Contains("block 1:") || l.Contains("block 3:"), "the good block builds and the skipped one is not built");
        lines.Should().Contain(l => l.Contains("1 error(s) in the guides' blocks, of 2 built"));
    }
}
