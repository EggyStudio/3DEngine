using System.Text;
using System.Text.Json;

namespace Engine.Cli;

/// <summary>Prints an envelope as JSON or as lines a person reads, and turns it into an exit code.</summary>
internal static class Output
{
    public static int Print(Options options, string envelope, Action<JsonElement>? human = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(envelope);
        }
        catch (JsonException error)
        {
            Console.Error.WriteLine($"e3d: the answer was not JSON: {error.Message}");
            return Exit.General;
        }

        using (document)
        {
            var root = document.RootElement;
            var success = root.TryGetProperty("success", out var flag) && flag.ValueKind == JsonValueKind.True;
            var code = Code(root);

            if (options.Json)
            {
                Console.WriteLine(Pretty(root));
                return success ? Exit.Ok : Exit.For(code);
            }

            if (!success && root.TryGetProperty("errors", out var errors))
                foreach (var error in errors.EnumerateArray())
                    Console.WriteLine($"error [{Text(error, "code")}] {Text(error, "message")}");

            if (!options.Quiet && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (human is not null) human(data);
                else if (data.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String)
                {
                    if (result.GetString() is { Length: > 0 } text) Console.WriteLine(text);
                }
                else Console.WriteLine(Pretty(data));
            }

            return success ? Exit.Ok : Exit.For(code);
        }
    }

    public static int Refuse(Options options, string command, string code, string message) =>
        Print(options, CliJson.Fail(command, code, message));

    public static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string Code(JsonElement root) =>
        root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? Text(errors[0], "code")
            : "";

    private static string Pretty(JsonElement element)
    {
        var buffer = new MemoryStream(512);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            element.WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
