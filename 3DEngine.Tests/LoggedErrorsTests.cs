using System.Reflection;
using FluentAssertions;
using Xunit.Sdk;

namespace Engine.Tests;

/// <summary>
/// The hook that fails a test for an error the engine logged during it (N 3.7), run here around
/// errors logged in its own instance's place, so these tests judge what it would have said.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoggedErrorsTests
{
    private static void Plain() { }

    [ExpectsError("Tests.Hook", "expected")]
    private static void Expecting() { }

    private static MethodInfo Method(string name) => typeof(LoggedErrorsTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    // What the hook says after a test of the method given, during which the action ran.
    private static string? Judge(string method, Action during)
    {
        var hook = new FailOnLoggedErrorsAttribute();
        hook.Before(Method(method));
        during();
        try
        {
            hook.After(Method(method));
            return null;
        }
        catch (XunitException ex)
        {
            return ex.Message;
        }
    }

    [Fact]
    public void An_Error_Logged_On_The_Tests_Thread_Fails_It_With_The_Errors_Text()
    {
        Judge(nameof(Plain), () => Log.Category("Tests.Hook").Error("it broke", new InvalidOperationException("inside")))
            .Should().Be("N 3.7: the engine logged 1 error during this test, the first: [Tests.Hook] it broke: InvalidOperationException: inside");
        Judge(nameof(Plain), () => Log.Category("Tests.Hook").Warn("only a warning")).Should().BeNull();
    }

    // On a thread of its own, since a task waited on may run on the waiting thread.
    private static void OnAnotherThread(Action action)
    {
        var thread = new Thread(() => action());
        thread.Start();
        thread.Join();
    }

    // On a thread that inherits nothing of this flow, as another test's is.
    private static void InAnotherFlow(Action action)
    {
        Thread thread;
        using (ExecutionContext.SuppressFlow())
        {
            thread = new Thread(() => action());
            thread.Start();
        }
        thread.Join();
    }

    [Fact]
    public void An_Error_Logged_On_A_Thread_Of_The_Tests_App_Is_The_Tests_And_Another_Apps_Is_Not()
    {
        Judge(nameof(Plain), () =>
        {
            using var app = new App();
            OnAnotherThread(() => Log.Category("Tests.Hook").Error("from a thread the app started"));
        }).Should().EndWith("[Tests.Hook] from a thread the app started");

        Judge(nameof(Plain), () => InAnotherFlow(() =>
        {
            using var other = new App();
            Log.Category("Tests.Hook").Error("from another test's app");
        })).Should().BeNull("an app made in another flow is another test's");
    }

    [Fact]
    public async Task An_Error_Logged_After_An_Await_By_An_App_Made_After_It_Is_The_Tests()
    {
        var hook = new FailOnLoggedErrorsAttribute();
        hook.Before(Method(nameof(Plain)));
        // The app made on a thread of the pool, as a test that awaits goes on on one.
        await Task.Yield();
        await Task.Run(() =>
        {
            using var app = new App();
            OnAnotherThread(() => Log.Category("Tests.Hook").Error("after an await"));
        });
        var judge = () => hook.After(Method(nameof(Plain)));

        judge.Should().Throw<XunitException>().WithMessage("*[Tests.Hook] after an await",
            "the test's ears went with it over the await, to the app it made after it");
    }

    // The same through the hook xUnit puts on every test, which fails this test where the error is
    // laid to no test or to another.
    [Fact]
    [ExpectsError("Tests.Hook", "an awaiting test's own")]
    public async Task An_Awaiting_Test_Hears_Its_Own_Apps_Errors()
    {
        await Task.Yield();
        await Task.Run(() =>
        {
            using var app = new App();
            OnAnotherThread(() => Log.Category("Tests.Hook").Error("an awaiting test's own"));
        });
    }

    [Fact]
    public void A_Test_Of_A_Failure_Passes_With_The_Error_It_Expects_And_Fails_Without_It()
    {
        Judge(nameof(Expecting), () => Log.Category("Tests.Hook.Inner").Error("the expected failure")).Should().BeNull();
        Judge(nameof(Expecting), () => { }).Should().Be("N 3.7: the test expects an error of Tests.Hook saying 'expected', and none was logged.");
        Judge(nameof(Expecting), () =>
        {
            Log.Category("Tests.Hook").Error("the expected failure");
            Log.Category("Tests.Hook").Error("and another");
        }).Should().EndWith("the first: [Tests.Hook] and another", "an error it does not expect still fails it");
    }
}
