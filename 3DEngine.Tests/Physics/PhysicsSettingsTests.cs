using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

[Trait("Category", "Unit")]
public class PhysicsSettingsTests
{
    [Fact]
    public void Defaults_Are_Sensible()
    {
        var s = new PhysicsSettings();

        s.Gravity.Should().Be(new Vector3(0f, -9.81f, 0f));
        s.FixedTimeStep.Should().BeApproximately(1f / 60f, 1e-6f);
        s.SubstepCount.Should().Be(1);
        s.VelocityIterations.Should().Be(8);
        s.UseFixedTimestep.Should().BeTrue();
        s.MaxStepsPerFrame.Should().Be(8);
        s.WorkerThreads.Should().Be(1);
    }

    [Fact]
    public void Properties_Are_Mutable()
    {
        var s = new PhysicsSettings
        {
            Gravity = new Vector3(0, -20f, 0),
            FixedTimeStep = 1f / 120f,
            SubstepCount = 4,
            VelocityIterations = 16,
            UseFixedTimestep = false,
            MaxStepsPerFrame = 4,
            WorkerThreads = 2,
        };

        s.Gravity.Y.Should().Be(-20f);
        s.SubstepCount.Should().Be(4);
        s.WorkerThreads.Should().Be(2);
        s.UseFixedTimestep.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class PhysicsMaterialTests
{
    [Fact]
    public void Default_Has_Friction_One_And_No_Restitution()
    {
        var m = PhysicsMaterial.Default;

        m.Friction.Should().Be(1f);
        m.Restitution.Should().Be(0f);
        m.LinearDamping.Should().Be(0f);
        m.AngularDamping.Should().Be(0f);
    }
}