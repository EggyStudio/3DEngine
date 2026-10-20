using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// CHEATSHEET.md against the flat API it describes, so a function added without its line, or a
/// line left for a function that is gone, fails the suite.
/// </summary>
/// <remarks>
/// <see cref="CheatsheetWriter"/> writes the page's lists from the source, and
/// <c>build/cheatsheet.sh</c> writes them again through this test with
/// <c>ENGINE_WRITE_CHEATSHEET</c> set. A function is also matched by its name and number of
/// parameters against the built engine, which tells overloads apart without spelling the
/// cheatsheet's types the way reflection names them, so a function the writer could not read from
/// the source fails too.
/// </remarks>
[Trait("Category", "Unit")]
public partial class CheatsheetTests
{
    [Fact]
    public void Every_Public_Function_Of_The_Flat_API_Has_Its_Line_And_Every_Line_A_Function()
    {
        var api = typeof(E3D).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => $"{m.Name}/{m.GetParameters().Length}")
            .ToHashSet();
        var listed = Listed(File.ReadAllLines(Path.Combine(RepoRoot(), "CHEATSHEET.md"))).ToHashSet();

        // Joined, so a failure names every function at once.
        string.Join(", ", api.Except(listed).Order()).Should().BeEmpty("each public function of E3D has its line in CHEATSHEET.md");
        string.Join(", ", listed.Except(api).Order()).Should().BeEmpty("each line of CHEATSHEET.md names a function E3D has");
    }

    [Fact]
    public void The_Lists_Are_What_The_Script_Writes_From_Each_Function_And_Its_Documentation()
    {
        var path = Path.Combine(RepoRoot(), "CHEATSHEET.md");
        var page = File.ReadAllText(path).ReplaceLineEndings("\n");
        var written = CheatsheetWriter.Write(RepoRoot(), page);
        if (Environment.GetEnvironmentVariable("ENGINE_WRITE_CHEATSHEET") == "1")
        {
            File.WriteAllText(path, written);
            page = written;
        }

        // Each line that differs, so the failure says which functions changed.
        var lines = page.Split('\n');
        var report = string.Join("\n", lines.Except(written.Split('\n')).Select(l => "- " + l)
            .Concat(written.Split('\n').Except(lines).Select(l => "+ " + l)).Take(40));
        report.Should().BeEmpty("CHEATSHEET.md's lists are written from the source by build/cheatsheet.sh");
        written.Should().Be(page, "the lists hold the same lines in the same order");
    }

    [Fact]
    public void A_List_Holds_Its_Files_Functions_In_Order_Each_With_The_First_Sentence_Of_Its_Summary_Wrapped()
    {
        using var folder = new TestFolder("engine-cheatsheet-");
        Directory.CreateDirectory(folder.File("3DEngine/Api"));
        File.WriteAllText(folder.File("3DEngine/Api/E3D.One.cs"), """
            namespace Engine;
            public static partial class E3D
            {
                /// <summary>Opens a <see cref="Thing"/> of <paramref name="size"/>. The rest is the guide's.</summary>
                public static void Open(int size, float scale = 1.5f, string? name = null) { }

                private static void Helper() { }

                // -- Closing

                /// <summary>Closes it, as <see cref="Open(int, float, string)"/> opened it, or <see langword="null"/>.</summary>
                public static bool Close<T>(ref T thing) => true;

                /// <inheritdoc cref="Close{T}(ref T)"/>
                public static bool Close() => true;

                /// <summary>
                /// Waits for every thing opened before it to close, however long that takes, and then
                /// gives back how many there were.
                /// </summary>
                public static int Wait() => 0;

                /// <summary>Spins a thing about its axis until it stops.</summary>
                public static void SpinAboutAnAxis(int thing, float speed, float axisX, float axisY, float axisZ, bool clockwise) { }
            }
            """);
        File.WriteAllText(folder.File("3DEngine/Api/E3D.Two.cs"), """
            namespace Engine;
            public static partial class E3D
            {
                /// <summary>Counts.</summary>
                public static int Count() => 0;
            }
            """);
        const string page = """
            # Cheatsheet

            <!-- written by build/cheatsheet.sh from E3D.Two.cs, E3D.One.cs -->
            ```csharp
            void Gone();
            ```

            Kept as it is.
            """;

        CheatsheetWriter.Write(folder.Path, page).Should().Be("""
            # Cheatsheet

            <!-- written by build/cheatsheet.sh from E3D.Two.cs, E3D.One.cs -->
            ```csharp
            int Count();                                             // Counts

            void Open(int size, float scale = 1.5f, string? name = null); // Opens a Thing of size

            bool Close<T>(ref T thing);                              // Closes it, as Open opened it, or null
            bool Close();                                            // Closes it, as Open opened it, or null
            int Wait();                                              // Waits for every thing opened before it to close, however
                                                                     // long that takes, and then gives back how many there were
            void SpinAboutAnAxis(int thing, float speed, float axisX, float axisY, float axisZ, bool clockwise);
                                                                     // Spins a thing about its axis until it stops
            ```

            Kept as it is.
            """);

        var unnamed = () => CheatsheetWriter.Write(folder.Path, page.Replace("E3D.Two.cs, ", ""));
        unnamed.Should().Throw<InvalidDataException>().WithMessage("No list of CHEATSHEET.md names E3D.Two.cs*");
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

    [GeneratedRegex(@"^(?:\([^)]*\)|[\w.<>\[\]?, ]+?)\s+(?<name>[A-Z]\w*|rl[A-Z]\w*)(?:<[^>]*>)?\((?<parameters>.*)\);\s*(?://.*)?$")]
    private static partial Regex Declaration();

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
