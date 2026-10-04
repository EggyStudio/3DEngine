using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Docs;

/// <summary>
/// Every link in the README and the guide under <c>docs/</c> reaches a file of this repository, and
/// every anchor a heading of it, so a page moved or renamed breaks the suite rather than the reader.
/// </summary>
/// <remarks>
/// A link to this repository on GitHub, as the README's are, since it is also the package's page on
/// nuget.org where a relative link goes nowhere, is followed to the file it names in the checkout.
/// A link to another site is left alone, since the suite runs with no network.
/// </remarks>
[Trait("Category", "Unit")]
public partial class DocumentLinkTests
{
    private const string Blob = "https://github.com/EggyStudio/3DEngine/blob/main/";
    private const string Tree = "https://github.com/EggyStudio/3DEngine/tree/main/";
    private const string Raw = "https://raw.githubusercontent.com/EggyStudio/3DEngine/main/";

    [Fact]
    public void Every_Link_In_The_Readme_And_The_Guide_Reaches_A_File_And_A_Heading()
    {
        var root = RepoRoot();
        var pages = Directory.GetFiles(Path.Combine(root, "docs"), "*.md").Append(Path.Combine(root, "README.md"));
        var broken = new List<string>();
        foreach (var page in pages)
            foreach (Match link in Link().Matches(File.ReadAllText(page)))
                if (Problem(root, page, link.Groups["target"].Value) is { } problem)
                    broken.Add($"{Path.GetRelativePath(root, page)}: {link.Groups["target"].Value} ({problem})");

        string.Join("\n", broken).Should().BeEmpty("every link reaches a file of the repository and every anchor a heading");
    }

    [Fact]
    public void A_Heading_Is_Linked_By_Its_Words_In_Lower_Case_Joined_By_Hyphens()
    {
        Slug("Window and timing").Should().Be("window-and-timing");
        Slug("Drawing in 3D and cameras").Should().Be("drawing-in-3d-and-cameras");
        Slug("A `Camera3D` and a world").Should().Be("a-camera3d-and-a-world");
    }

    // What is wrong with a link from a page, or null when it reaches what it names.
    private static string? Problem(string root, string page, string target)
    {
        var (path, anchor) = target.Split('#', 2) is [var p, var a] ? (p, a) : (target, null);
        string file;
        if (path.StartsWith(Blob)) file = Path.Combine(root, path[Blob.Length..]);
        else if (path.StartsWith(Tree)) file = Path.Combine(root, path[Tree.Length..]);
        else if (path.StartsWith(Raw)) file = Path.Combine(root, path[Raw.Length..]);
        else if (path.Contains("://") || path.StartsWith("mailto:")) return null;
        else if (path.Length == 0) file = page;
        else file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(page)!, path));

        if (!File.Exists(file) && !Directory.Exists(file)) return "no such file";
        if (anchor is null || !file.EndsWith(".md")) return null;
        var headings = File.ReadAllLines(file).Where(l => l.StartsWith('#')).Select(l => Slug(l.TrimStart('#').Trim()));
        return headings.Contains(anchor) ? null : "no such heading";
    }

    // GitHub's anchor for a heading: lower case, with spaces as hyphens and punctuation dropped.
    private static string Slug(string heading) =>
        new(heading.ToLowerInvariant().Replace(' ', '-').Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    [GeneratedRegex(@"\]\((?<target>[^)\s]+)\)")]
    private static partial Regex Link();

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
