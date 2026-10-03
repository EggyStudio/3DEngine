using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

[Trait("Category", "Unit")]
public class BehaviorContextTests
{
    // -- Constructor resolves resources --
    [Fact]
    public void Constructor_Resolves_All_Required_Resources()
    {
        using var world = new World();
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();
        var time = new Time();
        var input = new Input();
        using var physics = new PhysicsWorld();
        world.InsertResource(ecs);
        world.InsertResource(cmd);
        world.InsertResource(time);
        world.InsertResource(input);
        world.InsertResource(physics);
        var ctx = new BehaviorContext(world);
        ctx.World.Should().BeSameAs(world);
        ctx.Ecs.Should().BeSameAs(ecs);
        ctx.Cmd.Should().BeSameAs(cmd);
        ctx.Time.Should().BeSameAs(time);
        ctx.Input.Should().BeSameAs(input);
        ctx.Physics.Should().BeSameAs(physics);
    }

    [Fact]
    public void Constructor_Throws_When_Physics_Missing()
    {
        using var world = new World();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Time());
        world.InsertResource(new Input());
        var act = () => new BehaviorContext(world);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Throws_When_EcsWorld_Missing()
    {
        using var world = new World();
        using var physics = new PhysicsWorld();
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Time());
        world.InsertResource(new Input());
        world.InsertResource(physics);
        var act = () => new BehaviorContext(world);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Throws_When_EcsCommands_Missing()
    {
        using var world = new World();
        using var physics = new PhysicsWorld();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new Time());
        world.InsertResource(new Input());
        world.InsertResource(physics);
        var act = () => new BehaviorContext(world);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Throws_When_Time_Missing()
    {
        using var world = new World();
        using var physics = new PhysicsWorld();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Input());
        world.InsertResource(physics);
        var act = () => new BehaviorContext(world);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Throws_When_Input_Missing()
    {
        using var world = new World();
        using var physics = new PhysicsWorld();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Time());
        world.InsertResource(physics);
        var act = () => new BehaviorContext(world);
        act.Should().Throw<InvalidOperationException>();
    }

    // -- EntityId --
    [Fact]
    public void EntityId_Defaults_To_Zero()
    {
        using var owned = CreateContext();
        owned.Context.EntityId.Should().Be(0);
    }

    [Fact]
    public void EntityId_Is_Settable()
    {
        using var owned = CreateContext();
        owned.Context.EntityId = 42;
        owned.Context.EntityId.Should().Be(42);
    }

    // -- Res<T> --
    [Fact]
    public void Res_Returns_Resource_From_World()
    {
        using var world = new World();
        using var physics = new PhysicsWorld();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Time());
        world.InsertResource(new Input());
        world.InsertResource(physics);
        world.InsertResource("custom-resource");
        var ctx = new BehaviorContext(world);
        ctx.Res<string>().Should().Be("custom-resource");
    }

    [Fact]
    public void Res_Throws_When_Resource_Missing()
    {
        using var owned = CreateContext();
        var act = () => owned.Context.Res<double>();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- Helpers --

    private static OwnedContext CreateContext()
    {
        var world = new World();
        var physics = new PhysicsWorld();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new EcsCommands());
        world.InsertResource(new Time());
        world.InsertResource(new Input());
        world.InsertResource(physics);
        return new OwnedContext(world, physics);
    }

    /// <summary>Holds a freshly-built world + physics world so tests can <c>using</c> them and dispose deterministically.</summary>
    private sealed class OwnedContext : IDisposable
    {
        private readonly World _world;
        private readonly PhysicsWorld _physics;
        public BehaviorContext Context { get; }

        public OwnedContext(World world, PhysicsWorld physics)
        {
            _world = world;
            _physics = physics;
            Context = new BehaviorContext(world);
        }

        public void Dispose()
        {
            _physics.Dispose();
            _world.Dispose();
        }
    }
}