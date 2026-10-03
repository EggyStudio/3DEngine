using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class WorldTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void InsertResource_And_Resource_RoundTrips()
    {
        _world.InsertResource("hello");

        _world.Resource<string>().Should().Be("hello");
    }

    [Fact]
    public void Resource_Throws_When_Missing()
    {
        var act = () => _world.Resource<int>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*int*");
    }

    [Fact]
    public void InsertResource_Replaces_Existing()
    {
        _world.InsertResource("first");
        _world.InsertResource("second");

        _world.Resource<string>().Should().Be("second");
    }

    [Fact]
    public void TryGetResource_Returns_True_When_Present()
    {
        _world.InsertResource(42);

        _world.TryGetResource<int>(out var value).Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetResource_Returns_False_When_Missing()
    {
        _world.TryGetResource<double>(out _).Should().BeFalse();
    }

    [Fact]
    public void TryResource_Returns_Value_When_Present()
    {
        _world.InsertResource("test");

        _world.TryResource<string>().Should().Be("test");
    }

    [Fact]
    public void TryResource_Returns_Default_When_Missing()
    {
        _world.TryResource<string>().Should().BeNull();
    }

    [Fact]
    public void ContainsResource_Returns_True_When_Present()
    {
        _world.InsertResource(1.5);

        _world.ContainsResource<double>().Should().BeTrue();
    }

    [Fact]
    public void ContainsResource_Returns_False_When_Missing()
    {
        _world.ContainsResource<float>().Should().BeFalse();
    }

    [Fact]
    public void GetOrInsertResource_Inserts_When_Missing()
    {
        var result = _world.GetOrInsertResource("fallback");

        result.Should().Be("fallback");
        _world.Resource<string>().Should().Be("fallback");
    }

    [Fact]
    public void GetOrInsertResource_Returns_Existing_When_Present()
    {
        _world.InsertResource("existing");

        var result = _world.GetOrInsertResource("fallback");

        result.Should().Be("existing");
    }

    [Fact]
    public void GetOrInsertResource_Factory_Calls_Factory_Once()
    {
        int callCount = 0;
        var result = _world.GetOrInsertResource(() => { callCount++; return "created"; });

        result.Should().Be("created");
        callCount.Should().Be(1);

        // Second call should not invoke factory
        var result2 = _world.GetOrInsertResource(() => { callCount++; return "other"; });
        result2.Should().Be("created");
        callCount.Should().Be(1);
    }

    [Fact]
    public void InitResource_Creates_Default_When_Missing()
    {
        var result = _world.InitResource<List<int>>();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void InitResource_Returns_Existing_When_Present()
    {
        var existing = new List<int> { 1, 2, 3 };
        _world.InsertResource(existing);

        var result = _world.InitResource<List<int>>();

        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void RemoveResource_Returns_True_When_Present()
    {
        _world.InsertResource(42);

        _world.RemoveResource<int>().Should().BeTrue();
        _world.ContainsResource<int>().Should().BeFalse();
    }

    [Fact]
    public void RemoveResource_Returns_False_When_Missing()
    {
        _world.RemoveResource<int>().Should().BeFalse();
    }

    [Fact]
    public void ResourceCount_Reflects_Insertions_And_Removals()
    {
        _world.ResourceCount.Should().Be(0);

        _world.InsertResource("a");
        _world.InsertResource(42);
        _world.ResourceCount.Should().Be(2);

        _world.RemoveResource<string>();
        _world.ResourceCount.Should().Be(1);
    }

    [Fact]
    public void ResourceTypes_Returns_Snapshot_Of_Stored_Types()
    {
        _world.InsertResource("text");
        _world.InsertResource(3.14);

        var types = _world.ResourceTypes;

        types.Should().Contain(typeof(string));
        types.Should().Contain(typeof(double));
    }

    [Fact]
    public void Clear_Disposes_IDisposable_Resources()
    {
        var disposable = new TrackingDisposable();
        _world.InsertResource(disposable);

        _world.Clear();

        disposable.Disposed.Should().BeTrue();
        _world.ResourceCount.Should().Be(0);
    }

    [Fact]
    public void Clear_Swallows_Exceptions_During_Dispose()
    {
        _world.InsertResource(new ThrowingDisposable());
        _world.InsertResource("safe");

        // Should not throw - exceptions are swallowed
        var act = () => _world.Clear();
        act.Should().NotThrow();

        _world.ResourceCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var disposable = new TrackingDisposable();
        _world.InsertResource(disposable);

        _world.Dispose();
        _world.Dispose(); // Second call should not throw

        disposable.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void Concurrent_InsertResource_Does_Not_Corrupt_State()
    {
        Parallel.For(0, 100, i =>
        {
            var w = new World();
            w.InsertResource($"val-{i}");
            w.Resource<string>().Should().StartWith("val-");
            w.Dispose();
        });
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount;
        public bool Disposed => DisposeCount > 0;
        public void Dispose() => DisposeCount++;
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("boom");
    }
}