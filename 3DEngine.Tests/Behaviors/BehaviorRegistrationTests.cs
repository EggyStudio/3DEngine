using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

public static class DummyRegistration
{
    public static int Calls;

    [GeneratedBehaviorRegistration]
    public static void Register(App app)
    {
        Calls++;
    }
}

[Trait("Category", "Unit")]
public class BehaviorRegistrationTests
{
    [Fact]
    public void BehaviorsPlugin_Invokes_Annotated_Registration_Methods()
    {
        using var app = new App();
        var callsBefore = DummyRegistration.Calls;

        new BehaviorsPlugin().Build(app);

        DummyRegistration.Calls.Should().BeGreaterThan(callsBefore);
    }
}
