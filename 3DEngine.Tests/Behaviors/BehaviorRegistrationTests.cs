using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

public static class DummyRegistration
{
    public static int Calls;

    public static void Register(App app)
    {
        Calls++;
    }
}

[Trait("Category", "Unit")]
public class BehaviorRegistrationTests
{
    [Fact]
    public void BehaviorsPlugin_Invokes_The_Registrations_Added_To_The_List()
    {
        GeneratedBehaviors.Add(DummyRegistration.Register);
        using var app = new App();
        var callsBefore = DummyRegistration.Calls;

        new BehaviorsPlugin { ScriptsDirectory = null }.Build(app);

        DummyRegistration.Calls.Should().Be(callsBefore + 1);
    }

    [Fact]
    public void An_Assembly_With_Behaviors_Adds_Its_Generated_Registration_As_It_Loads()
    {
        GeneratedBehaviors.All.Should().Contain(r => r.Method.DeclaringType!.Assembly == typeof(App).Assembly
                                                     && r.Method.Name == "Register" && r.Method.DeclaringType.Name == "BehaviorRegistration",
            "the generator's module initializer adds the engine's own registration, for its debug HUDs, found by no search of its types");
    }
}
