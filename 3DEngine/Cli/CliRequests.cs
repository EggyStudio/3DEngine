using System.Collections.Concurrent;

namespace Engine;

/// <summary>A request the socket thread received, waiting for the main thread to answer it.</summary>
internal sealed class CliRequest(string operation, string? line, string? id)
{
    private readonly TaskCompletionSource<string> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Operation { get; } = operation;
    public string? Line { get; } = line;
    public string? Id { get; } = id;
    public Task<string> Answer => _answer.Task;

    /// <summary>The command a held request ran, for the envelope it is answered with.</summary>
    public string? Holding { get; set; }

    /// <summary>The answer a held request gives once released.</summary>
    public string? Held { get; set; }

    /// <summary>The frame a held request is released at.</summary>
    public ulong Release { get; set; }

    /// <summary>Asked each frame for a request answering later, until it returns an answer.</summary>
    public Func<string?>? Poll { get; set; }

    public void Complete(string envelope) => _answer.TrySetResult(envelope);
}

/// <summary>
/// Requests from the socket thread, answered on the main thread by <see cref="Pump"/>, so the
/// socket never touches the world.
/// </summary>
internal sealed class CliQueue
{
    private readonly ConcurrentQueue<CliRequest> _arrived = new();
    private readonly List<CliRequest> _held = [];

    public void Add(CliRequest request) => _arrived.Enqueue(request);

    /// <summary>Answers what arrived, and releases held requests whose frame has come.</summary>
    public void Pump(World world, App app)
    {
        while (_arrived.TryDequeue(out var request))
        {
            var envelope = CliDispatch.Answer(request, world, app);
            if (envelope is null) _held.Add(request);
            else request.Complete(envelope);
        }

        if (_held.Count == 0) return;

        var frame = CliDispatch.Frame(world);
        for (var index = _held.Count - 1; index >= 0; index--)
        {
            var request = _held[index];

            if (request.Poll is { } poll)
            {
                string? answer;
                CliError? failure;
                using (ConsoleHost.Lend(world, app))
                {
                    answer = poll();
                    failure = ConsoleHost.Failure;
                }

                if (answer is not null)
                {
                    request.Held = answer;
                    request.Complete(failure is { } error
                        ? CliJson.Envelope(request.Operation, success: false, errors: [error], id: request.Id)
                        : CliDispatch.Release(request, frame));
                    _held.RemoveAt(index);
                }
                else if (frame >= request.Release)
                {
                    request.Complete(CliJson.Fail(request.Operation, "TIMEOUT",
                        $"'{request.Holding}' had no answer after {ConsoleHost.LaterFrames} frames.", request.Id));
                    _held.RemoveAt(index);
                }
                continue;
            }

            if (frame < request.Release) continue;
            request.Complete(CliDispatch.Release(request, frame));
            _held.RemoveAt(index);
        }
    }

    /// <summary>Answers everything still waiting with a refusal, because the app is closing.</summary>
    public void Abandon()
    {
        while (_arrived.TryDequeue(out var request))
            request.Complete(CliJson.Fail(request.Operation, "SESSION_CLOSING", "The app is shutting down.", request.Id));

        foreach (var request in _held)
            request.Complete(CliJson.Fail(request.Operation, "SESSION_CLOSING",
                "The app shut down before the frame this was waiting for.", request.Id));
        _held.Clear();
    }
}
