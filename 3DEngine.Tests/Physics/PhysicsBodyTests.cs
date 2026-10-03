using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

/// <summary>
/// Unit tests for <see cref="PhysicsBody"/> using a real <see cref="PhysicsWorld"/>.
/// Gravity is disabled so velocity / pose assertions are deterministic.
/// </summary>
[Trait("Category", "Unit")]
public class PhysicsBodyTests
{
    private static PhysicsWorld NewWorld() => 
        new(new PhysicsSettings { Gravity = Vector3.Zero });

    [Fact]
    public void Default_Body_Is_Not_Valid()
    {
        var body = default(PhysicsBody);

        body.IsValid.Should().BeFalse();
        body.World.Should().BeNull();
    }

    [Fact]
    public void IsValid_True_When_World_Reports_Existing()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f);

        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Destroy_Removes_Body_From_World()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f);

        body.Destroy();

        body.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Position_Get_And_Set_Roundtrip_Through_World()
    {
        using var w = NewWorld();
        var body = w.CreateBox(new Vector3(1, 2, 3), Vector3.One);

        body.Position.Should().Be(new Vector3(1, 2, 3));

        body.Position = new Vector3(10, 20, 30);

        body.Position.Should().Be(new Vector3(10, 20, 30));
    }

    [Fact]
    public void Rotation_Get_And_Set_Roundtrip_Through_World()
    {
        using var w = NewWorld();
        var body = w.CreateBox(Vector3.Zero, Vector3.One);
        var q = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f));

        body.Rotation = q;

        var read = body.Rotation;
        read.X.Should().BeApproximately(q.X, 1e-4f);
        read.Y.Should().BeApproximately(q.Y, 1e-4f);
        read.Z.Should().BeApproximately(q.Z, 1e-4f);
        read.W.Should().BeApproximately(q.W, 1e-4f);
    }

    [Fact]
    public void SetLinearVelocity_Wakes_Body_And_Stores_Value()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f);
        body.Sleep();

        body.SetLinearVelocity(new Vector3(0, 5, 0));

        body.IsAwake.Should().BeTrue();
        body.Velocity.Y.Should().BeApproximately(5f, 1e-3f);
    }

    [Fact]
    public void SetAngularVelocity_Stores_Value()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f);

        body.SetAngularVelocity(new Vector3(1, 2, 3));

        body.AngularVelocity.X.Should().BeApproximately(1f, 1e-3f);
        body.AngularVelocity.Y.Should().BeApproximately(2f, 1e-3f);
        body.AngularVelocity.Z.Should().BeApproximately(3f, 1e-3f);
    }

    [Fact]
    public void ApplyImpulse_Imparts_Linear_Velocity_For_Unit_Mass()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f, mass: 1f);

        body.ApplyImpulse(new Vector3(0, 5, 0));

        body.Velocity.Y.Should().BeApproximately(5f, 1e-2f);
    }

    [Fact]
    public void ApplyImpulseAtPoint_Produces_Both_Linear_And_Angular_Velocity()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(new Vector3(10, 0, 0), 1f, mass: 1f);

        body.ApplyImpulseAtPoint(new Vector3(0, 1, 0), worldPoint: new Vector3(11, 0, 0));

        body.Velocity.Y.Should().BeApproximately(1f, 1e-2f);
        body.AngularVelocity.Length().Should().BeGreaterThan(0f);
    }

    [Fact]
    public void ApplyAngularImpulse_Imparts_Angular_Velocity()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f, mass: 1f);

        body.ApplyAngularImpulse(new Vector3(0, 0, 1f));

        body.AngularVelocity.Z.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Wake_And_Sleep_Toggle_State()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 1f);

        body.IsAwake.Should().BeTrue();
        body.Sleep();
        body.IsAwake.Should().BeFalse();
        body.Wake();
        body.IsAwake.Should().BeTrue();
    }

    [Fact]
    public void Static_Body_Velocities_Are_Always_Zero()
    {
        using var w = NewWorld();
        var body = w.CreateStaticBox(Vector3.Zero, Vector3.One);

        body.Kind.Should().Be(BodyKind.Static);
        body.Velocity.Should().Be(Vector3.Zero);
        body.AngularVelocity.Should().Be(Vector3.Zero);
        body.IsAwake.Should().BeFalse();
    }

    [Fact]
    public void Equality_Compares_World_Handle_And_Kind()
    {
        using var w = NewWorld();
        var a = new PhysicsBody(w, 7, BodyKind.Dynamic);
        var b = new PhysicsBody(w, 7, BodyKind.Dynamic);
        var c = new PhysicsBody(w, 7, BodyKind.Static);

        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == c).Should().BeFalse();
    }
}