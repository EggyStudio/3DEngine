using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>The client half of the CLI: one request to a serving app, one envelope back.</summary>
internal static class CliClient
{
    /// <summary>Sends an operation to <paramref name="session"/> and returns its envelope.</summary>
    /// <remarks>A connection that fails is answered with a <c>SESSION_UNREACHABLE</c> envelope rather than an exception.</remarks>
    public static string Send(CliSession session, string operation, string? line = null, double seconds = 30)
    {
        try
        {
            using var caller = new TcpClient();
            var milliseconds = (int)Math.Clamp(seconds * 1000, 1000, int.MaxValue);
            caller.SendTimeout = milliseconds;
            caller.ReceiveTimeout = milliseconds;
            caller.Connect("127.0.0.1", session.Port);
            caller.NoDelay = true;

            using var stream = caller.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            writer.WriteLine(Request(session.Token, operation, line, seconds));

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadLine() ?? CliJson.Fail(operation, "SESSION_UNREACHABLE",
                $"{session.Name} ({session.Pid}) accepted the connection and then said nothing. It may have exited mid-request.");
        }
        catch (Exception error) when (error is SocketException or IOException)
        {
            return CliJson.Fail(operation, "SESSION_UNREACHABLE",
                $"Could not reach {session.Name} ({session.Pid}) on port {session.Port}: {error.Message}. It may have exited.");
        }
    }

    // The seconds the caller waits go with the request, so the app waits as long for the answer
    // and a long frames.wait on a slow device is not cut short.
    private static string Request(string token, string operation, string? line, double seconds)
    {
        var buffer = new MemoryStream(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("op", operation);
            writer.WriteString("token", token);
            if (line is not null) writer.WriteString("line", line);
            writer.WriteNumber("seconds", seconds);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
