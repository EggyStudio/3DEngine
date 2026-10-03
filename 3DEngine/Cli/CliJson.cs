using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>An error in a CLI envelope: a code a script can branch on, and a sentence a person can read.</summary>
public readonly record struct CliError(string Code, string Message);

/// <summary>
/// Writes the one shape every CLI answer takes:
/// <c>{"id"?, "success", "command", "data": {...} | null, "errors": [{"code", "message"}], "warnings": []}</c>.
/// </summary>
/// <remarks>Written by hand with <see cref="Utf8JsonWriter"/>, so no serializer reflects over types.</remarks>
public static class CliJson
{
    /// <summary>Builds an envelope.</summary>
    public static string Envelope(string command, bool success, Action<Utf8JsonWriter>? data = null,
        IReadOnlyList<CliError>? errors = null, IReadOnlyList<string>? warnings = null, string? id = null)
    {
        var buffer = new MemoryStream(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (id is not null) writer.WriteString("id", id);
            writer.WriteBoolean("success", success);
            writer.WriteString("command", command);

            if (data is null)
            {
                writer.WriteNull("data");
            }
            else
            {
                writer.WritePropertyName("data");
                writer.WriteStartObject();
                data(writer);
                writer.WriteEndObject();
            }

            writer.WritePropertyName("errors");
            writer.WriteStartArray();
            foreach (var error in errors ?? [])
            {
                writer.WriteStartObject();
                writer.WriteString("code", error.Code);
                writer.WriteString("message", error.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("warnings");
            writer.WriteStartArray();
            foreach (var warning in warnings ?? []) writer.WriteStringValue(warning);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>A successful envelope.</summary>
    public static string Ok(string command, Action<Utf8JsonWriter>? data = null, string? id = null) =>
        Envelope(command, success: true, data, id: id);

    /// <summary>A failed envelope with one error.</summary>
    public static string Fail(string command, string code, string message, string? id = null) =>
        Envelope(command, success: false, errors: [new CliError(code, message)], id: id);
}
