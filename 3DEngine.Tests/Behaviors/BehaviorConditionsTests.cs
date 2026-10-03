using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

[Trait("Category", "Unit")]
public class BehaviorConditionsTests
{
    // -- SystemToggleRegistry --

    [Fact]
    public void ToggleRegistry_Get_Returns_Default_When_Unset()
    {
        var reg = new SystemToggleRegistry();

        reg.Get("sys.A").Should().BeTrue();
        reg.Get("sys.A", defaultEnabled: false).Should().BeFalse();
    }

    [Fact]
    public void ToggleRegistry_Flip_Toggles_State()
    {
        var reg = new SystemToggleRegistry();

        // Default is true → flip → false
        reg.Flip("sys.A");
        reg.Get("sys.A").Should().BeFalse();

        // Flip again → true
        reg.Flip("sys.A");
        reg.Get("sys.A").Should().BeTrue();
    }

    [Fact]
    public void ToggleRegistry_Flip_Respects_DefaultEnabled()
    {
        var reg = new SystemToggleRegistry();

        // Default false → flip → true
        reg.Flip("sys.B", defaultEnabled: false);
        reg.Get("sys.B", defaultEnabled: false).Should().BeTrue();
    }

    [Fact]
    public void ToggleRegistry_Independent_Ids()
    {
        var reg = new SystemToggleRegistry();

        reg.Flip("sys.A");

        reg.Get("sys.A").Should().BeFalse();
        reg.Get("sys.B").Should().BeTrue(); // untouched
    }

    // -- HasResource --

    [Fact]
    public void HasResource_Returns_True_When_Present()
    {
        using var world = new World();
        world.InsertResource("hello");

        var condition = BehaviorConditions.HasResource<string>();

        condition(world).Should().BeTrue();
    }

    [Fact]
    public void HasResource_Returns_False_When_Missing()
    {
        using var world = new World();

        var condition = BehaviorConditions.HasResource<string>();

        condition(world).Should().BeFalse();
    }

    // -- ResourceIs --

    [Fact]
    public void ResourceIs_Returns_True_When_Predicate_Passes()
    {
        using var world = new World();
        world.InsertResource(42);

        var condition = BehaviorConditions.ResourceIs<int>(v => v > 10);

        condition(world).Should().BeTrue();
    }

    [Fact]
    public void ResourceIs_Returns_False_When_Predicate_Fails()
    {
        using var world = new World();
        world.InsertResource(5);

        var condition = BehaviorConditions.ResourceIs<int>(v => v > 10);

        condition(world).Should().BeFalse();
    }

    [Fact]
    public void ResourceIs_Returns_False_When_Resource_Missing()
    {
        using var world = new World();

        var condition = BehaviorConditions.ResourceIs<int>(v => v > 0);

        condition(world).Should().BeFalse();
    }

    // -- AnyWithComponent --

    [Fact]
    public void AnyWithComponent_Returns_True_When_Entities_Exist()
    {
        using var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 1 });

        var condition = BehaviorConditions.AnyWithComponent<TestComp>();

        condition(world).Should().BeTrue();
    }

    [Fact]
    public void AnyWithComponent_Returns_False_When_No_Entities()
    {
        using var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);

        var condition = BehaviorConditions.AnyWithComponent<TestComp>();

        condition(world).Should().BeFalse();
    }

    // -- ModifiersHeld --

    [Fact]
    public void ModifiersHeld_None_Always_Returns_True()
    {
        var input = new Input();

        BehaviorConditions.ModifiersHeld(input, KeyModifier.None).Should().BeTrue();
    }

    [Fact]
    public void ModifiersHeld_Ctrl_Requires_LeftOrRightCtrl()
    {
        var input = new Input();

        BehaviorConditions.ModifiersHeld(input, KeyModifier.Ctrl).Should().BeFalse();

        input.SetKey(Key.LCtrl, true);
        BehaviorConditions.ModifiersHeld(input, KeyModifier.Ctrl).Should().BeTrue();
    }

    [Fact]
    public void ModifiersHeld_Shift_Requires_LeftOrRightShift()
    {
        var input = new Input();

        BehaviorConditions.ModifiersHeld(input, KeyModifier.Shift).Should().BeFalse();

        input.SetKey(Key.RShift, true);
        BehaviorConditions.ModifiersHeld(input, KeyModifier.Shift).Should().BeTrue();
    }

    [Fact]
    public void ModifiersHeld_Alt_Requires_LeftOrRightAlt()
    {
        var input = new Input();

        BehaviorConditions.ModifiersHeld(input, KeyModifier.Alt).Should().BeFalse();

        input.SetKey(Key.LAlt, true);
        BehaviorConditions.ModifiersHeld(input, KeyModifier.Alt).Should().BeTrue();
    }

    [Fact]
    public void ModifiersHeld_Combined_Requires_All()
    {
        var input = new Input();

        var combined = KeyModifier.Ctrl | KeyModifier.Shift;

        // Only Ctrl held → not enough
        input.SetKey(Key.LCtrl, true);
        BehaviorConditions.ModifiersHeld(input, combined).Should().BeFalse();

        // Both held → passes
        input.SetKey(Key.LShift, true);
        BehaviorConditions.ModifiersHeld(input, combined).Should().BeTrue();
    }

    // -- KeyToggle (string overload) --

    [Fact]
    public void KeyToggle_Returns_DefaultEnabled_When_Key_Not_Pressed()
    {
        using var world = new World();
        world.InsertResource(new Input());

        var condition = BehaviorConditions.KeyToggle("sys.X", Key.F3, defaultEnabled: true);

        condition(world).Should().BeTrue();
    }

    [Fact]
    public void KeyToggle_Flips_On_Key_Press()
    {
        using var world = new World();
        var input = new Input();
        input.SetKey(Key.F3, true); // simulate F3 pressed this frame
        world.InsertResource(input);

        var condition = BehaviorConditions.KeyToggle("sys.X", Key.F3, defaultEnabled: true);

        // First call: F3 is pressed → flips true→false
        condition(world).Should().BeFalse();
    }
}
