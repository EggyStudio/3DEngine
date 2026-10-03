namespace Engine;

public sealed partial class App
{
    /// <summary>
    /// Adds and builds a plugin. Each concrete plugin type can only be added once;
    /// duplicate registrations are silently skipped.
    /// </summary>
    /// <param name="plugin">The plugin instance to register and build.</param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <exception cref="Exception">Re-throws any exception thrown by the plugin's <see cref="IPlugin.Build"/> method.</exception>
    /// <remarks>
    /// Plugins are identified by their concrete <see cref="Type"/>. Calling <c>AddPlugin</c> twice
    /// with the same plugin type is a no-op. The plugin's <see cref="IPlugin.Build"/> method is
    /// invoked immediately during this call - not deferred.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.AddPlugin(new TimePlugin())
    ///    .AddPlugin(new InputPlugin())
    ///    .AddPlugin(new EcsPlugin());
    /// </code>
    /// </example>
    /// <seealso cref="IPlugin"/>
    /// <seealso cref="HasPlugin{T}"/>
    public App AddPlugin(IPlugin plugin)
    {
        var pluginType = plugin.GetType();
        var pluginName = pluginType.Name;

        if (_plugins.ContainsKey(pluginType))
        {
            Logger.Trace($"Plugin already registered, skipping: {pluginName}");
            return this;
        }

        // Validate declared plugin dependencies before Build() - converts a future
        // NullReferenceException deep inside another plugin into a clear, typed error
        // pointing at the actual ordering bug.
        foreach (var dep in plugin.Dependencies)
        {
            if (!_plugins.ContainsKey(dep))
            {
                var ex = new PluginOrderException(pluginName, dep.Name, dep.Name);
                Logger.Error($"Plugin '{pluginName}' rejected: missing dependency '{dep.Name}'.", ex);
                throw ex;
            }
        }

        Logger.Info($"Adding plugin: {pluginName}...");
        _plugins[pluginType] = plugin;

        try
        {
            plugin.Build(this);
            Logger.Info($"Plugin built: {pluginName} (total plugins: {_plugins.Count})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Plugin '{pluginName}' failed during Build()", ex);
            throw;
        }

        return this;
    }

    /// <summary>Checks whether a plugin of type <typeparamref name="T"/> has been registered.</summary>
    /// <typeparam name="T">The concrete plugin type to look up.</typeparam>
    /// <returns><c>true</c> if a plugin of type <typeparamref name="T"/> has been added; otherwise <c>false</c>.</returns>
    public bool HasPlugin<T>() where T : IPlugin => 
        _plugins.ContainsKey(typeof(T));

    /// <summary>Number of plugins currently registered.</summary>
    /// <returns>The count of registered plugins.</returns>
    public int PluginCount => _plugins.Count;

    /// <summary>Snapshot of all registered plugin types at the time of the call.</summary>
    /// <returns>A read-only collection of <see cref="Type"/> objects representing each registered plugin.</returns>
    public IReadOnlyCollection<Type> Plugins => _plugins.Keys.ToArray();

    /// <summary>
    /// Adds every plugin in the supplied <see cref="IPluginGroup"/>, sorted by
    /// <see cref="IPlugin.Order"/> ascending (stable: ties keep the group's declaration order).
    /// Each plugin is then forwarded to <see cref="AddPlugin"/>, which still performs the usual
    /// <see cref="IPlugin.Dependencies"/> validation.
    /// </summary>
    /// <param name="group">The plugin group to register.</param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This is the recommended entry point for bundles like <c>DefaultPlugins</c>: foundational
    /// plugins (<see cref="PluginOrder.Foundation"/>) automatically build before their consumers,
    /// so consumers no longer need to declare them in <see cref="IPlugin.Dependencies"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.AddPlugins(new DefaultPlugins());
    /// </code>
    /// </example>
    /// <seealso cref="IPluginGroup"/>
    /// <seealso cref="IPlugin.Order"/>
    public App AddPlugins(IPluginGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // OrderBy is a stable sort, so plugins that share an Order keep their
        // declaration order from GetPlugins() - which matches user expectation
        // when reading the source-level list top-to-bottom.
        var ordered = group.GetPlugins().OrderBy(p => p.Order).ToList();
        Logger.Info($"AddPlugins: '{group.GetType().Name}' contributing {ordered.Count} plugin(s) (sorted by Order).");

        foreach (var plugin in ordered)
            AddPlugin(plugin);

        return this;
    }
}