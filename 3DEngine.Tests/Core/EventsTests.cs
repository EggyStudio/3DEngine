using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class EventsTests
{
    [Fact]
    public void Send_And_Read_RoundTrips()
    {
        var events = new Events<int>();

        events.Send(42);

        events.Read().Should().Equal(42);
    }

    [Fact]
    public void Read_Returns_Empty_When_No_Events()
    {
        var events = new Events<int>();

        events.Read().Should().BeEmpty();
    }

    [Fact]
    public void Send_Multiple_Preserves_Order()
    {
        var events = new Events<string>();

        events.Send("a");
        events.Send("b");
        events.Send("c");

        events.Read().Should().Equal("a", "b", "c");
    }

    // -- SendBatch --

    [Fact]
    public void SendBatch_Appends_All_Events()
    {
        var events = new Events<int>();
        ReadOnlySpan<int> batch = [10, 20, 30];

        events.SendBatch(batch);

        events.Read().Should().Equal(10, 20, 30);
    }

    [Fact]
    public void Drain_Returns_Events_And_Clears_Buffer()
    {
        var events = new Events<int>();
        events.Send(1);
        events.Send(2);

        var drained = events.Drain();

        drained.Should().Equal(1, 2);
        events.IsEmpty.Should().BeTrue();
        events.Read().Should().BeEmpty();
    }

    [Fact]
    public void Drain_Returns_Empty_When_No_Events()
    {
        var events = new Events<int>();

        events.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Clear_Removes_All_Events()
    {
        var events = new Events<int>();
        events.Send(1);
        events.Send(2);

        events.Clear();

        events.Count.Should().Be(0);
        events.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Count_Reflects_Number_Of_Events()
    {
        var events = new Events<int>();

        events.Count.Should().Be(0);
        events.IsEmpty.Should().BeTrue();

        events.Send(1);

        events.Count.Should().Be(1);
        events.IsEmpty.Should().BeFalse();
    }

    // -- World Extension Methods --

    [Fact]
    public void WorldEventExtensions_SendEvent_And_ReadEvents_Work()
    {
        using var world = new World();

        world.SendEvent("hello");
        world.SendEvent("world");

        world.ReadEvents<string>().Should().Equal("hello", "world");
    }

    [Fact]
    public void WorldEventExtensions_DrainEvents_Clears_Buffer()
    {
        using var world = new World();

        world.SendEvent(42);
        var drained = world.DrainEvents<int>();

        drained.Should().Equal(42);
        world.ReadEvents<int>().Should().BeEmpty();
    }

    [Fact]
    public void WorldEventExtensions_ClearEvents_Empties_Buffer()
    {
        using var world = new World();

        world.SendEvent(1);
        world.ClearEvents<int>();

        world.ReadEvents<int>().Should().BeEmpty();
    }

    [Fact]
    public void Events_Get_AutoCreates_Queue_In_World()
    {
        using var world = new World();

        var queue = Events.Get<int>(world);

        queue.Should().NotBeNull();
        world.ContainsResource<Events<int>>().Should().BeTrue();
    }

    [Fact]
    public void Events_Get_Returns_Same_Instance()
    {
        using var world = new World();

        var a = Events.Get<int>(world);
        var b = Events.Get<int>(world);

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Concurrent_Sends_Do_Not_Lose_Events()
    {
        var events = new Events<int>();
        const int count = 1000;

        Parallel.For(0, count, i => events.Send(i));

        events.Count.Should().Be(count);
    }
}
