namespace Engine;

public sealed partial class App
{
    /// <summary>
    /// Returns the world resource of type <typeparamref name="T"/>, or throws a
    /// <see cref="PluginOrderException"/> with a message identifying the requesting plugin
    /// and the plugin that should have been added first.
    /// </summary>
    /// <typeparam name="T">The resource type required by the caller.</typeparam>
    /// <param name="callerPlugin">
    /// Name of the plugin requiring the resource - typically <c>nameof(MyPlugin)</c>. Surfaced in the error message.
    /// </param>
    /// <param name="providingPlugin">
    /// Optional name of the plugin that is expected to provide <typeparamref name="T"/>
    /// (e.g. <c>nameof(AppWindowPlugin)</c>). When supplied, the error message tells the user
    /// exactly which plugin to register before <paramref name="callerPlugin"/>.
    /// </param>
    /// <returns>The existing resource instance.</returns>
    /// <exception cref="PluginOrderException">
    /// Thrown when no resource of type <typeparamref name="T"/> is present in the <see cref="World"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// public void Build(App app)
    /// {
    ///     var window = app.RequireResource&lt;AppWindow&gt;(nameof(SdlImGuiPlugin), nameof(AppWindowPlugin));
    ///     // ... use window ...
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PluginOrderException"/>
    /// <seealso cref="IPlugin.Dependencies"/>
    public T RequireResource<T>(string callerPlugin, string? providingPlugin = null) where T : notnull
    {
        if (World.TryGetResource<T>(out var value))
            return value;

        throw new PluginOrderException(callerPlugin, typeof(T).Name, providingPlugin);
    }
}