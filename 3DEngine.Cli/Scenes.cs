namespace Engine.Cli;

/// <summary>
/// The scene packs from a terminal, the manifests in the checkout's <c>scenes</c> folder and a pack
/// fetched and unpacked into the user's cache folder (<see cref="ScenePacks"/>).
/// </summary>
/// <remarks>
/// A fetch tells its progress on the error stream, for a person watching, and answers on the output
/// stream as every verb does.
/// </remarks>
internal static class Scenes
{
    /// <summary>Runs <c>scenes</c> or <c>scenes fetch &lt;name&gt;</c>.</summary>
    public static int Run(Options options, string[] rest) => rest switch
    {
        [] or ["list"] => List(options),
        ["fetch", var name] => Fetch(options, name),
        _ => Output.Refuse(options, "scenes", "BAD_ARGUMENT", "Run 'e3d scenes' to list the packs, or 'e3d scenes fetch <name>' to fetch one."),
    };

    private static string Manifests => Path.Combine(Repo.Root ?? Environment.CurrentDirectory, "scenes");

    private static int List(Options options)
    {
        var packs = ScenePacks.List(Manifests, out var problems);
        return Output.Print(options, CliJson.Ok("scenes", writer =>
        {
            writer.WriteString("folder", ScenePacks.Folder);
            writer.WriteStartArray("packs");
            foreach (var pack in packs)
            {
                writer.WriteStartObject();
                writer.WriteString("name", pack.Name);
                writer.WriteString("title", pack.Title);
                writer.WriteString("license", pack.License);
                writer.WriteNumber("size", pack.Size);
                writer.WriteBoolean("fetched", ScenePacks.IsFetched(pack));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("problems");
            foreach (var problem in problems) writer.WriteStringValue(problem);
            writer.WriteEndArray();
        }), data =>
        {
            foreach (var pack in data.GetProperty("packs").EnumerateArray())
            {
                var fetched = pack.GetProperty("fetched").GetBoolean() ? "fetched" : "not fetched";
                Console.WriteLine($"{Output.Text(pack, "name"),-24} {pack.GetProperty("size").GetInt64() / 1_000_000,6} MB  {fetched,-12} {Output.Text(pack, "title")}");
            }
            foreach (var problem in data.GetProperty("problems").EnumerateArray()) Console.WriteLine(problem.GetString());
            Console.WriteLine($"kept in {Output.Text(data, "folder")}");
        });
    }

    private static int Fetch(Options options, string name)
    {
        var file = Path.Combine(Manifests, name + ".json");
        if (!File.Exists(file))
            return Output.Refuse(options, "scenes", "NOT_FOUND", $"There is no scene pack {name}, its manifest {file} not there. Run 'e3d scenes' for the packs.");
        if (!ScenePacks.TryRead(file, out var pack, out var problem))
            return Output.Refuse(options, "scenes", "BAD_MANIFEST", problem);

        if (!ScenePacks.IsFetched(pack))
        {
            // Told in whole percents, each once, which is a line a second or so at a home's speed.
            var said = -1;
            var progress = new Progress<double>(fraction =>
            {
                var percent = (int)(fraction * 100);
                if (percent == said || options.Quiet) return;
                said = percent;
                Console.Error.Write($"\rfetching {pack.Title}, {percent}%");
            });
            var failed = ScenePacks.FetchAsync(pack, progress).GetAwaiter().GetResult();
            if (said >= 0) Console.Error.WriteLine();
            if (failed is not null) return Output.Refuse(options, "scenes", "FAILED", failed);
        }

        return Output.Print(options, CliJson.Ok("scenes", writer =>
        {
            writer.WriteString("name", pack.Name);
            writer.WriteString("model", ScenePacks.ModelOf(pack));
            writer.WriteString("attribution", pack.Attribution);
        }), data => Console.WriteLine($"{pack.Title} is fetched and checked, its model {Output.Text(data, "model")}. {pack.Attribution}"));
    }
}
