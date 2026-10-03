namespace Engine;

/// <summary>
/// Plugin contract for extending the application.
/// Plugins insert resources, register systems, and compose other plugins during
/// the one-time <see cref="Build"/> phase before the main loop starts.
/// </summary>
/// <remarks>
/// Plugins are the primary composition unit for the engine. Each plugin encapsulates a
/// self-contained feature (e.g., time tracking, input, rendering) and is added to the
/// <see cref="App"/> via <see cref="App.AddPlugin"/>. The <see cref="Build"/> method is
/// called exactly once during application setup, before any systems execute.
/// </remarks>
/// <example>
/// <code>
/// public class PhysicsPlugin : IPlugin
/// {
///     public void Build(App app)
///     {
///         app.World.InitResource&lt;PhysicsWorld&gt;();
///         app.AddSystem(Stage.PreUpdate, new SystemDescriptor(PhysicsSystem.Step, "Physics.Step")
///             .Write&lt;PhysicsWorld&gt;());
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="App"/>
/// <seealso cref="App.AddPlugin"/>
public interface IPlugin
{
    /// <summary>Called once during app setup to configure resources, systems, and sub-plugins.</summary>
    /// <param name="app">The application builder to register resources and systems with.</param>
    void Build(App app);

    /// <summary>
    /// Other plugin types that must be registered with the <see cref="App"/> before this plugin's
    /// <see cref="Build"/> is invoked. Default is empty (no dependencies).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="App.AddPlugin"/> validates this list before calling <see cref="Build"/> and throws
    /// a <see cref="PluginOrderException"/> with a clear, actionable message when a dependency is
    /// missing - replacing the obscure <see cref="NullReferenceException"/> that would otherwise
    /// surface deep inside another plugin's <c>Build()</c>.
    /// </para>
    /// <para>
    /// Only declare <b>hard</b> dependencies here (resources read via <c>World.Resource&lt;T&gt;()</c>
    /// during <see cref="Build"/>). Soft, optional dependencies (handled with <c>TryGetResource</c>)
    /// must <b>not</b> be listed - that lets minimal apps omit them.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class SdlImGuiPlugin : IPlugin
    /// {
    ///     public IReadOnlyCollection&lt;Type&gt; Dependencies { get; } = [typeof(AppWindowPlugin)];
    ///     public void Build(App app) { /* safely reads AppWindow */ }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PluginOrderException"/>
    IReadOnlyCollection<Type> Dependencies => Array.Empty<Type>();

    /// <summary>
    /// Unity-style execution order. <b>Lower values run first.</b> Default is
    /// <see cref="PluginOrder.Default"/> (0). Foundational plugins that other plugins
    /// implicitly rely on (e.g. <c>AssetPlugin</c>) should set a negative value such as
    /// <see cref="PluginOrder.Foundation"/> so they always build first when added as part
    /// of an <see cref="IPluginGroup"/> - eliminating the need for every consumer to
    /// declare them in <see cref="Dependencies"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>When this matters:</b> <see cref="App.AddPlugins(IPluginGroup)"/> sorts the group's
    /// plugins by this property (stable sort: ties keep declaration order) before invoking
    /// <see cref="App.AddPlugin"/> on each. Plain <see cref="App.AddPlugin"/> calls are still
    /// eager and use the order they're written in.
    /// </para>
    /// <para>
    /// <b>Use <see cref="Dependencies"/>, not <c>Order</c>, for true type-level constraints</b>
    /// (e.g. <c>SdlImGuiPlugin</c> needs <c>AppWindowPlugin</c>). Use <c>Order</c> only to
    /// express "I'm foundational" / "I'm late".
    /// </para>
    /// </remarks>
    /// <seealso cref="PluginOrder"/>
    /// <seealso cref="IPluginGroup"/>
    int Order => PluginOrder.Default;
}

/// <summary>
/// Named priority bands for <see cref="IPlugin.Order"/>. Lower values build first.
/// </summary>
/// <remarks>
/// Use these instead of bare integers so the intent is self-documenting and so
/// foundational plugins added by third parties can slot themselves into the same band
/// without colliding with engine internals.
/// </remarks>
public static class PluginOrder
{
    /// <summary>Reserved for the absolute earliest plugins (e.g. exception handlers).</summary>
    public const int Earliest = -2000;

    /// <summary>
    /// Foundational subsystems other plugins implicitly depend on
    /// (e.g. <c>AssetPlugin</c>, future <c>JobSystemPlugin</c>).
    /// </summary>
    public const int Foundation = -1000;

    /// <summary>Default for normal feature plugins.</summary>
    public const int Default = 0;

    /// <summary>Plugins that should build after most others (e.g. high-level glue).</summary>
    public const int Late = 1000;

    /// <summary>Reserved for the absolute last plugins.</summary>
    public const int Latest = 2000;
}