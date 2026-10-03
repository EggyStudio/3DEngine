using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class PluginDependencyTests : IDisposable
{
    private readonly App _app = new();

    public void Dispose() => _app.Dispose();

    private sealed class ProviderPlugin : IPlugin
    {
        public void Build(App app) => app.InsertResource(new Marker());
    }

    private sealed class ConsumerWithDeclaredDep : IPlugin
    {
        public IReadOnlyCollection<Type> Dependencies { get; } = [typeof(ProviderPlugin)];
        public bool BuiltWithMarker;
        public void Build(App app)
        {
            BuiltWithMarker = app.World.ContainsResource<Marker>();
        }
    }

    private sealed class ConsumerWithoutDeclaredDep : IPlugin
    {
        public void Build(App app)
        {
            // Reads the resource directly via RequireResource so an ordering bug
            // surfaces as a typed PluginOrderException, not an NRE.
            app.RequireResource<Marker>(nameof(ConsumerWithoutDeclaredDep), nameof(ProviderPlugin));
        }
    }

    private sealed class Marker { }

    [Fact]
    public void AddPlugin_With_Satisfied_Declared_Dependency_Builds_Successfully()
    {
        var consumer = new ConsumerWithDeclaredDep();

        _app.AddPlugin(new ProviderPlugin())
            .AddPlugin(consumer);

        consumer.BuiltWithMarker.Should().BeTrue();
    }

    [Fact]
    public void AddPlugin_With_Missing_Declared_Dependency_Throws_Typed_Exception()
    {
        var act = () => _app.AddPlugin(new ConsumerWithDeclaredDep());

        act.Should()
           .Throw<PluginOrderException>()
           .Where(ex => ex.RequiringPlugin == nameof(ConsumerWithDeclaredDep))
           .Where(ex => ex.MissingDependency == nameof(ProviderPlugin))
           .Where(ex => ex.SuggestedPlugin == nameof(ProviderPlugin));
    }

    [Fact]
    public void AddPlugin_Reordered_Reports_Wrong_Plugin_Not_NullReference()
    {
        // The user-facing scenario: someone reorders plugins and the build breaks.
        // The error must point at the actual culprit, not surface as a NullReferenceException.
        var act = () => _app.AddPlugin(new ConsumerWithoutDeclaredDep());

        act.Should().Throw<PluginOrderException>();
        act.Should().NotThrow<NullReferenceException>();
    }

    [Fact]
    public void RequireResource_Returns_Existing_Resource()
    {
        _app.InsertResource(new Marker());

        var marker = _app.RequireResource<Marker>("CallerPlugin");

        marker.Should().NotBeNull();
    }

    [Fact]
    public void RequireResource_Throws_PluginOrderException_With_Caller_And_Suggestion()
    {
        var act = () => _app.RequireResource<Marker>("MyPlugin", "ProviderPlugin");

        act.Should()
           .Throw<PluginOrderException>()
           .Where(ex => ex.RequiringPlugin == "MyPlugin")
           .Where(ex => ex.MissingDependency == nameof(Marker))
           .Where(ex => ex.SuggestedPlugin == "ProviderPlugin");
    }

    [Fact]
    public void Default_Dependencies_Is_Empty_For_Plugins_That_Do_Not_Override()
    {
        var plugin = new ProviderPlugin();

        ((IPlugin)plugin).Dependencies.Should().BeEmpty();
    }

    // -- Order / IPluginGroup --

    private sealed class FoundationPlugin : IPlugin
    {
        public int Order => PluginOrder.Foundation;
        public void Build(App app) => app.InsertResource(new Marker());
    }

    private sealed class DefaultOrderPlugin : IPlugin
    {
        public bool BuiltAfterFoundation;
        public void Build(App app) => BuiltAfterFoundation = app.World.ContainsResource<Marker>();
    }

    private sealed class TestGroup(IPlugin[] plugins) : IPluginGroup
    {
        public IEnumerable<IPlugin> GetPlugins() => plugins;
    }

    [Fact]
    public void AddPlugins_Sorts_By_Order_So_Foundation_Builds_First()
    {
        // Listed in the "wrong" order on purpose: consumer first, foundation second.
        var consumer = new DefaultOrderPlugin();
        var foundation = new FoundationPlugin();
        var group = new TestGroup([consumer, foundation]);

        _app.AddPlugins(group);

        // Foundation should have been built first because of its lower Order,
        // even though it was listed second in the group.
        consumer.BuiltAfterFoundation.Should().BeTrue();
        _app.Plugins.Should().Contain([typeof(FoundationPlugin), typeof(DefaultOrderPlugin)]);
    }

    [Fact]
    public void AddPlugins_Stable_Sort_Preserves_Declaration_Order_Within_Same_Order_Band()
    {
        RecordingPlugin.BuildLog.Clear();
        var group = new TestGroup([new RecA(), new RecB(), new RecC()]);

        _app.AddPlugins(group);

        RecordingPlugin.BuildLog.Should().Equal("A", "B", "C");
    }

    private abstract class RecordingPlugin : IPlugin
    {
        public static readonly List<string> BuildLog = new();
        protected abstract string Name { get; }
        public void Build(App app) => BuildLog.Add(Name);
    }
    private sealed class RecA : RecordingPlugin { protected override string Name => "A"; }
    private sealed class RecB : RecordingPlugin { protected override string Name => "B"; }
    private sealed class RecC : RecordingPlugin { protected override string Name => "C"; }

    [Fact]
    public void Default_Order_Is_PluginOrderDefault()
    {
        var p = new ProviderPlugin();
        ((IPlugin)p).Order.Should().Be(PluginOrder.Default);
    }
}