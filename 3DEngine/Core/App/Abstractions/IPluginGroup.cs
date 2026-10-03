namespace Engine;

/// <summary>
/// A bundle of plugins that should be added together. The <see cref="App"/> sorts the
/// group's plugins by <see cref="IPlugin.Order"/> (stable; ties keep declaration order)
/// before invoking <see cref="App.AddPlugin"/> on each.
/// </summary>
/// <remarks>
/// <para>
/// Use a plugin group when several plugins ship as a logical unit (e.g. <c>DefaultPlugins</c>)
/// and at least one of them is foundational. Tagging the foundational plugin with
/// <see cref="PluginOrder.Foundation"/> via <see cref="IPlugin.Order"/> guarantees it builds
/// before its consumers regardless of where it appears in the group's source-level list.
/// </para>
/// <para>
/// Plugins inside a group can still declare <see cref="IPlugin.Dependencies"/> for hard
/// type-level relationships; those are validated by <see cref="App.AddPlugin"/> after sorting.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyPlugins : IPluginGroup
/// {
///     public IEnumerable&lt;IPlugin&gt; GetPlugins() =>
///     [
///         new TexturesPlugin(),  // Order = Default → would normally run later
///         new AssetPlugin(),     // Order = Foundation → sorts to the front automatically
///         new MaterialPlugin(),
///     ];
/// }
/// app.AddPlugins(new MyPlugins());
/// </code>
/// </example>
/// <seealso cref="IPlugin.Order"/>
/// <seealso cref="PluginOrder"/>
/// <seealso cref="App.AddPlugins(IPluginGroup)"/>
public interface IPluginGroup
{
    /// <summary>Returns the plugins in this group in their natural (source-level) order.</summary>
    /// <remarks>
    /// The returned sequence is enumerated once. Order matters only as a tiebreaker:
    /// <see cref="App.AddPlugins(IPluginGroup)"/> sorts by <see cref="IPlugin.Order"/> first.
    /// </remarks>
    IEnumerable<IPlugin> GetPlugins();
}