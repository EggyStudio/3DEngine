using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Sdk;

[assembly: Engine.Tests.FailOnLoggedErrorsAttribute]

namespace Engine.Tests;

/// <summary>
/// Fails a test during which the engine logged an error the test did not say it expects (N 3.7), as
/// a test that draws fails for an error of the validation layer.
/// </summary>
/// <remarks>
/// <para>
/// Tests run side by side and the log is the process's, so an error is laid to a test by the app
/// that logged it. An app is the test's when the test's flow made it while the test ran, over any
/// awaits, and a line logged anywhere in an app's work, on a task or a thread the app started,
/// carries the app (<see cref="App.Current"/>). A line logged in the test's own flow is the test's
/// as well.
/// What an app made in a test class's constructor logs from another thread, and what a class's
/// Dispose logs, comes after the test is judged and is not read.
/// </para>
/// <para>
/// A test of a failure names each error it expects with <see cref="ExpectsErrorAttribute"/>, and
/// fails where one does not come. A test on <c>build/norm/3.7.txt</c> is a place to mend, passed
/// over while it logs and failed once it no longer does, so the list only gets shorter. With
/// <c>E3D_LOGGED_ERRORS</c> naming a file, each test that logged an error it did not expect is
/// written there with the first of them, and none fails for it, which is how the list was made.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class FailOnLoggedErrorsAttribute : BeforeAfterTestAttribute
{
    private sealed class Heard
    {
        // An exception kept as its type's name and its message, since a type a script defines would
        // keep the script's load context from unloading for as long as the test runs.
        public readonly ConcurrentQueue<(string Category, string Message, string? Exception, string? ExceptionMessage)> Errors = new();
        public volatile bool Closed;
    }

    // The ears of the test whose flow this is, which follow it over its awaits to whichever thread
    // goes on with it, and into the threads and tasks it starts. xUnit calls Before from a method
    // that is not async, so what Before sets here is the test's.
    private static readonly AsyncLocal<Heard?> Ears = new();
    private static readonly ConditionalWeakTable<App, Heard> Apps = new();
    private static readonly string? Survey = Environment.GetEnvironmentVariable("E3D_LOGGED_ERRORS");
    private static readonly Lazy<HashSet<string>> Listed = new(() =>
    {
        var path = Path.Combine(Api.CheatsheetTests.RepoRoot(), "build", "norm", "3.7.txt");
        return File.Exists(path)
            ? File.ReadLines(path).Where(line => line.Length > 0 && !line.StartsWith('#')).Select(line => line.Split('\t')[0]).ToHashSet(StringComparer.Ordinal)
            : [];
    });

    private Heard? _heard;

    static FailOnLoggedErrorsAttribute()
    {
        App.Created += app =>
        {
            if (Ears.Value is { } heard) Apps.AddOrUpdate(app, heard);
        };
        Log.ErrorLogged += (category, message, exception) =>
        {
            var heard = App.Current is { } app && Apps.TryGetValue(app, out var made) ? made : Ears.Value;
            if (heard is { Closed: false }) heard.Errors.Enqueue((category, message, exception?.GetType().Name, exception?.Message));
        };
    }

    public override void Before(MethodInfo methodUnderTest) => Ears.Value = _heard = new Heard();

    public override void After(MethodInfo methodUnderTest)
    {
        var heard = _heard;
        _heard = null;
        if (ReferenceEquals(Ears.Value, heard)) Ears.Value = null;
        if (heard is null) return;
        heard.Closed = true;

        var expected = methodUnderTest.GetCustomAttributes<ExpectsErrorAttribute>().ToList();
        var errors = heard.Errors.ToList();
        var unexpected = errors.Where(error => !expected.Any(e => e.Matches(error.Category, error.Message, error.ExceptionMessage))).ToList();
        var missing = expected.Where(e => !errors.Any(error => e.Matches(error.Category, error.Message, error.ExceptionMessage))).ToList();
        var name = $"{methodUnderTest.DeclaringType?.FullName}.{methodUnderTest.Name}";

        if (Survey is not null)
        {
            if (unexpected.Count > 0)
                lock (Listed) File.AppendAllText(Survey, $"{name}\t{Text(unexpected[0])}\n");
            return;
        }

        if (missing.Count > 0)
            throw new XunitException($"N 3.7: the test expects an error of {missing[0].Category} saying '{missing[0].Part}', and none was logged.");
        if (Listed.Value.Contains(name))
        {
            if (unexpected.Count == 0)
                throw new XunitException($"N 3.7: {name} is on build/norm/3.7.txt and logged no error, so off the list it comes.");
            return;
        }
        if (unexpected.Count > 0)
            throw new XunitException($"N 3.7: the engine logged {unexpected.Count} error{(unexpected.Count == 1 ? "" : "s")} during this test, the first: {Text(unexpected[0])}");
    }

    private static string Text((string Category, string Message, string? Exception, string? ExceptionMessage) error) =>
        $"[{error.Category}] {error.Message}" + (error.Exception is { } type ? $": {type}: {error.ExceptionMessage}" : "");
}

/// <summary>
/// An error a test of a failure expects the engine to log, by its category, or the start of it, and
/// a part of its message or its exception's message (N 3.7).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExpectsErrorAttribute(string category, string part) : Attribute
{
    public string Category { get; } = category;
    public string Part { get; } = part;

    internal bool Matches(string category, string message, string? exceptionMessage) =>
        category.StartsWith(Category, StringComparison.Ordinal)
        && (message.Contains(Part, StringComparison.Ordinal) || (exceptionMessage?.Contains(Part, StringComparison.Ordinal) ?? false));
}
