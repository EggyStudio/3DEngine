using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// A loopback TCP listener on a port the system picks, answering one JSON line with one JSON line
/// on a connection that may carry many. Every request carries the session's token.
/// </summary>
/// <remarks>
/// A request is <c>{"op":"run|list|status|ping","token":"...","line":"entity.count","id":"..."}</c>.
/// The socket thread only parses and queues it. <see cref="CliQueue.Pump"/> answers it on the main
/// thread between frames, and the socket thread waits for that up to the seconds the request
/// carries, or <see cref="Patience"/> for one that carries none.
/// </remarks>
internal sealed class CliServer : IDisposable
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly CliQueue _queue;
    private readonly TcpListener _listener;
    private readonly AppThreads _threads;
    private volatile bool _stopping;

    // The connections open, closed as the server stops, so the threads reading them end and the
    // app's shutdown can join them.
    private readonly HashSet<TcpClient> _callers = [];

    public CliServer(CliQueue queue, AppThreads threads)
    {
        _queue = queue;
        _threads = threads;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _threads.Start("e3d-cli-accept", Accept);
    }

    public int Port { get; }

    public string Token { get; }

    private void Accept()
    {
        while (!_stopping)
        {
            TcpClient caller;
            try
            {
                caller = _listener.AcceptTcpClient();
            }
            catch (Exception error) when (error is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            lock (_callers)
            {
                if (_stopping)
                {
                    caller.Dispose();
                    return;
                }
                _callers.Add(caller);
            }
            _threads.Start("e3d-cli-connection", () => Serve(caller));
        }
    }

    private void Serve(TcpClient caller)
    {
        using (caller)
        {
            try
            {
                caller.NoDelay = true;
                using var stream = caller.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                while (!_stopping && reader.ReadLine() is { } line)
                {
                    if (line.Trim().Length == 0) continue;
                    writer.WriteLine(Handle(line));
                }
            }
            catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
            {
                // The caller hung up, or the server closed the connection as it stopped, which are
                // the ordinary ways for one to end.
            }
            finally
            {
                lock (_callers) _callers.Remove(caller);
            }
        }
    }

    private string Handle(string line)
    {
        string operation;
        string? command, id, token;
        var patience = Patience;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            operation = root.TryGetProperty("op", out var op) ? op.GetString() ?? "run" : "run";
            command = root.TryGetProperty("line", out var body) ? body.GetString() : null;
            id = root.TryGetProperty("id", out var given) ? given.GetString() : null;
            token = root.TryGetProperty("token", out var carried) ? carried.GetString() : null;
            if (root.TryGetProperty("seconds", out var seconds) && seconds.TryGetDouble(out var wait) && wait > 0)
                patience = TimeSpan.FromSeconds(Math.Min(wait, 24 * 60 * 60));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return CliJson.Fail("request", "BAD_REQUEST",
                "That was not a JSON object. One request per line, as {\"op\":\"run\",\"token\":\"...\",\"line\":\"app.status\"}.");
        }

        // Compared in fixed time, because the token is all that stands between this port and any
        // other process on the machine that finds it.
        if (token is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(Token)))
            return CliJson.Fail(operation, "BAD_TOKEN", "The token did not match this session's. It is in the session file 'e3d status' reads.", id);

        var request = new CliRequest(operation, command, id);
        _queue.Add(request);

        return request.Answer.Wait(patience)
            ? request.Answer.Result
            : CliJson.Fail(operation, "TIMEOUT",
                $"The app did not answer within {patience.TotalSeconds:0} seconds. It may be stalled, or no longer running frames, and --timeout gives it longer.", id);
    }

    public void Dispose()
    {
        if (_stopping) return;
        _stopping = true;
        try { _listener.Stop(); }
        catch (SocketException) { }
        // A connection's thread waits on a read until the caller writes or hangs up, so the
        // connections still open are closed for it.
        lock (_callers)
        {
            foreach (var caller in _callers) caller.Dispose();
            _callers.Clear();
        }
    }
}
