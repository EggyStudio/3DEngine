using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Engine.Tests.Core;

/// <summary>
/// The objects on the GC's heap by type, read by <c>dotnet-gcdump</c> from the process it is taken
/// in, where <c>E3D_GCDUMP</c> names the tool, so a leak test that fails on a machine no one here
/// has says which types a closed app left behind.
/// </summary>
/// <remarks>
/// The tool's report gives each type an object's size and a count, and an array's size is one of
/// its objects' where they differ, so the bytes a type grew by are a guess and its count is not.
/// A census taken first is alive at the second, so the types it is kept in are named for it and
/// left out of what grew, and the strings that grew count its names, a few dozen KB.
/// </remarks>
internal sealed partial class HeapCensus
{
    private static readonly string? Tool =
        Environment.GetEnvironmentVariable("E3D_GCDUMP") is { Length: > 0 } path && File.Exists(path) ? path : null;

    // A line of the report: an object's size, the count, the type with the band of sizes a large
    // object falls in, and its assembly.
    [GeneratedRegex(@"^\s*([\d,]+)\s+([\d,]+)\s+(.+?)(?:\s+\(Bytes > [^)]*\))?\s+\[[^\]]*\]\s*$")]
    private static partial Regex Row();

    // The namespaces of a type's name and of its arguments', left out so more types fit a line.
    [GeneratedRegex(@"\b(?:\w+\.)+(?=\w)")]
    private static partial Regex Namespaces();

    // A type's objects, in a type of the census's own, so its own containers name it.
    private readonly record struct Counted(long Count, long Bytes);

    private readonly Dictionary<string, Counted> _types;

    private HeapCensus(Dictionary<string, Counted> types) => _types = types;

    /// <summary>Whether a census can be taken, the tool being named.</summary>
    public static bool Available => Tool is not null;

    /// <summary>The heap's types now, or why there are none, the tool missing or failing.</summary>
    public static (HeapCensus? Census, string? Failure) Take()
    {
        if (Tool is null) return (null, "E3D_GCDUMP names no dotnet-gcdump");
        using var folder = new TestFolder("engine-census-");
        var file = folder.File("heap.gcdump");
        var (collected, collectOutput) = Run(Tool, "collect", "-p", Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "-o", file);
        if (collected != 0 || !File.Exists(file)) return (null, $"dotnet-gcdump collect ended with {collected}, {collectOutput.Trim()}");
        var (reported, report) = Run(Tool, "report", file);
        if (reported != 0) return (null, $"dotnet-gcdump report ended with {reported}, {report.Trim()}");

        var types = new Dictionary<string, Counted>(StringComparer.Ordinal);
        foreach (var line in report.Split('\n'))
        {
            if (Row().Match(line) is not { Success: true } row) continue;
            var size = long.Parse(row.Groups[1].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            var count = long.Parse(row.Groups[2].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            var name = Namespaces().Replace(row.Groups[3].Value, "");
            // A type's bands of sizes are one type here.
            var had = types.GetValueOrDefault(name);
            types[name] = new Counted(had.Count + count, had.Bytes + size * count);
        }
        return types.Count > 0 ? (new HeapCensus(types), null) : (null, "dotnet-gcdump's report had no line of a type");
    }

    /// <summary>
    /// The types that grew most since <paramref name="before"/>, by the bytes they grew by, at most
    /// <paramref name="count"/> of them, each with how many more there are.
    /// </summary>
    public string GrownSince(HeapCensus before, int count)
    {
        var grown = _types
            .Select(entry =>
            {
                var had = before._types.GetValueOrDefault(entry.Key);
                return (Name: entry.Key, Count: entry.Value.Count - had.Count, Bytes: entry.Value.Bytes - had.Bytes);
            })
            .Where(type => type.Count > 0 && type.Bytes > 0 && !type.Name.Contains(nameof(HeapCensus), StringComparison.Ordinal))
            .OrderByDescending(type => type.Bytes)
            .Take(count)
            .Select(type => $"{type.Name} +{type.Count:N0} about {type.Bytes / 1024.0:0} KB");
        return string.Join(", ", grown) is { Length: > 0 } text ? text : "none";
    }

    // Runs the tool and waits up to a minute, both its streams read as it writes them so neither
    // fills and stops it.
    private static (int Exit, string Output) Run(string tool, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{tool} did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(1)))
        {
            process.Kill(entireProcessTree: true);
            return (-1, "it did not end within a minute");
        }
        return (process.ExitCode, output.Result + error.Result);
    }
}
