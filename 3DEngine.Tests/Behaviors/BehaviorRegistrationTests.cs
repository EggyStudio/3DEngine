using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

// How many times it was called for each app, kept apart since the list is the process's and other
// tests build the plugin at the same time. The apps are held weakly, since the registration stays
// in the list and is called for every app built after this test, which a list of them kept for
// good, each with its device, a thousand apps in a run of the suite.
public static class DummyRegistration
{
    public static readonly ConditionalWeakTable<App, StrongBox<int>> Calls = new();

    public static void Register(App app) => Interlocked.Increment(ref Calls.GetOrCreateValue(app).Value);
}

[Trait("Category", "Unit")]
public class BehaviorRegistrationTests
{
    [Fact]
    public void BehaviorsPlugin_Invokes_The_Registrations_Added_To_The_List()
    {
        GeneratedBehaviors.Add(DummyRegistration.Register);
        using var app = new App();

        new BehaviorsPlugin { ScriptsDirectory = null }.Build(app);

        DummyRegistration.Calls.TryGetValue(app, out var calls).Should().BeTrue();
        calls!.Value.Should().Be(1);
    }

    [Fact]
    public void An_Assembly_With_Behaviors_Adds_Its_Generated_Registration_As_It_Loads()
    {
        GeneratedBehaviors.All.Should().Contain(r => r.Method.DeclaringType!.Assembly == typeof(App).Assembly
                                                     && r.Method.Name == "Register" && r.Method.DeclaringType.Name == "BehaviorRegistration",
            "the generator's module initializer adds the engine's own registration, for its debug HUDs, found by no search of its types");
    }
}
