using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// CHEATSHEET.md against the flat API it describes, so a function added without its line, or a
/// line left for a function that is gone, fails the suite.
/// </summary>
/// <remarks>
/// A function is matched by its name and number of parameters, which tells overloads apart without
/// spelling the cheatsheet's types the way reflection names them.
/// </remarks>
[Trait("Category", "Unit")]
public partial class CheatsheetTests
{
    [Fact]
    public void Every_Public_Function_Of_The_Flat_API_Has_Its_Line_And_Every_Line_A_Function()
    {
        var api = typeof(Engine3D).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => $"{m.Name}/{m.GetParameters().Length}")
            .ToHashSet();
        var listed = Listed(File.ReadAllLines(Path.Combine(RepoRoot(), "CHEATSHEET.md"))).ToHashSet();

        // Joined, so a failure names every function at once.
        string.Join(", ", api.Except(listed).Order()).Should().BeEmpty("each public function of Engine3D has its line in CHEATSHEET.md");
        string.Join(", ", listed.Except(api).Order()).Should().BeEmpty("each line of CHEATSHEET.md names a function Engine3D has");
    }

    [Fact]
    public void A_Line_Is_Read_As_Its_Name_And_Its_Number_Of_Parameters()
    {
        Listed([
            "```csharp",
            "void InitWindow(int width, int height, string title);    // Open a window",
            "(int A, int B) Pair();                                   // A tuple",
            "T Get<T>(Dictionary<int, string> map, float x = 1);      // Commas inside brackets",
            "```",
            "void Prose(int a);",
        ]).Should().Equal("InitWindow/3", "Pair/0", "Get/2");
    }

    // The functions the cheatsheet's C# blocks list, one a line, as name/parameter count.
    private static IEnumerable<string> Listed(IEnumerable<string> lines)
    {
        var inCode = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCode = line.StartsWith("```csharp", StringComparison.Ordinal) && !inCode;
                continue;
            }
            if (!inCode || Declaration().Match(line) is not { Success: true } match) continue;
            yield return $"{match.Groups["name"].Value}/{Count(match.Groups["parameters"].Value)}";
        }
    }

    // Parameters are separated by the commas outside brackets.
    private static int Count(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return 0;
        int depth = 0, count = 1;
        foreach (var c in parameters)
        {
            if (c is '<' or '(' or '[') depth++;
            else if (c is '>' or ')' or ']') depth--;
            else if (c == ',' && depth == 0) count++;
        }
        return count;
    }

    [GeneratedRegex(@"^(?:\([^)]*\)|[\w.<>\[\]?, ]+?)\s+(?<name>[A-Z]\w*)(?:<[^>]*>)?\((?<parameters>.*)\);\s*(?://.*)?$")]
    private static partial Regex Declaration();

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
