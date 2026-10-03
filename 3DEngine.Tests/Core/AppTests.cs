using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class AppTests : IDisposable
{
    private readonly App _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void AddPlugin_Builds_And_Registers_Plugin()
    {
        var plugin = new TestPlugin();

        _app.AddPlugin(plugin);

        _app.HasPlugin<TestPlugin>().Should().BeTrue();
        _app.PluginCount.Should().BeGreaterThanOrEqualTo(1);
        plugin.BuildCalled.Should().BeTrue();
    }

    [Fact]
    public void AddPlugin_Duplicate_Is_Skipped()
    {
        var plugin = new TestPlugin();

        _app.AddPlugin(plugin);
        _app.AddPlugin(plugin); // same instance, same type

        plugin.BuildCount.Should().Be(1);
    }

    [Fact]
    public void HasPlugin_Returns_False_For_Unregistered()
    {
        _app.HasPlugin<TestPlugin>().Should().BeFalse();
    }

    [Fact]
    public void Plugins_Returns_Snapshot_Of_Types()
    {
        _app.AddPlugin(new TestPlugin());

        _app.Plugins.Should().Contain(typeof(TestPlugin));
    }

    [Fact]
    public void AddPlugin_Fluent_Chaining()
    {
        var result = _app.AddPlugin(new TestPlugin());

        result.Should().BeSameAs(_app);
    }

    [Fact]
    public void AddSystem_Registers_To_Schedule()
    {
        _app.AddSystem(Stage.Update, _ => { });

        _app.Schedule.SystemCount(Stage.Update).Should().Be(1);
    }

    [Fact]
    public void AddSystem_Fluent_Chaining()
    {
        var result = _app.AddSystem(Stage.Update, _ => { });

        result.Should().BeSameAs(_app);
    }

    [Fact]
    public void AddSystem_With_RunCondition_Registers()
    {
        _app.AddSystem(Stage.Update, _ => { }, _ => true);

        _app.Schedule.SystemCount(Stage.Update).Should().Be(1);
    }

    [Fact]
    public void AddSystem_Descriptor_Registers()
    {
        var desc = new SystemDescriptor(_ => { }, "Test");
        _app.AddSystem(Stage.Update, desc);

        _app.Schedule.SystemCount(Stage.Update).Should().Be(1);
    }

    [Fact]
    public void InsertResource_Fluent_And_Accessible_Via_World()
    {
        var result = _app.InsertResource("test");

        result.Should().BeSameAs(_app);
        _app.World.Resource<string>().Should().Be("test");
    }

    [Fact]
    public void GetOrInsertResource_Works()
    {
        var val = _app.GetOrInsertResource("hello");

        val.Should().Be("hello");
        _app.World.Resource<string>().Should().Be("hello");
    }

    [Fact]
    public void InitResource_Works()
    {
        var list = _app.InitResource<List<int>>();

        list.Should().NotBeNull();
        list.Should().BeEmpty();
    }

    [Fact]
    public void World_Contains_Config_And_Diagnostics_By_Default()
    {
        _app.World.ContainsResource<Config>().Should().BeTrue();
        _app.World.ContainsResource<ScheduleDiagnostics>().Should().BeTrue();
    }

    [Fact]
    public void FrameCount_Starts_At_Zero()
    {
        _app.FrameCount.Should().Be(0UL);
    }

    [Fact]
    public void Dispose_Multiple_Times_Is_Safe()
    {
        var act = () =>
        {
            _app.Dispose();
            _app.Dispose();
        };

        act.Should().NotThrow();
    }

    private sealed class TestPlugin : IPlugin
    {
        public int BuildCount;
        public bool BuildCalled => BuildCount > 0;

        public void Build(App app)
        {
            BuildCount++;
        }
    }
}