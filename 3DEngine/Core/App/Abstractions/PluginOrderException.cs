namespace Engine;

/// <summary>
/// Thrown when a plugin is built before one of its declared dependencies is registered,
/// or when <see cref="App.RequireResource{T}"/> cannot find a required world resource.
/// </summary>
/// <remarks>
/// This replaces the obscure <see cref="NullReferenceException"/> that would otherwise
/// surface deep inside another plugin's <c>Build()</c> method when registration order
/// is wrong. The message always names the offending plugin and the missing dependency
/// or resource so the caller can fix the order at the call site.
/// </remarks>
/// <seealso cref="IPlugin.Dependencies"/>
/// <seealso cref="App.AddPlugin"/>
/// <seealso cref="App.RequireResource{T}"/>
public sealed class PluginOrderException : InvalidOperationException
{
    /// <summary>Name of the plugin whose build was attempted.</summary>
    public string RequiringPlugin { get; }

    /// <summary>Name of the missing dependency: a plugin type name or a resource type name.</summary>
    public string MissingDependency { get; }

    /// <summary>Optional hint identifying the plugin that should have been added first.</summary>
    public string? SuggestedPlugin { get; }

    /// <inheritdoc />
    public PluginOrderException(string requiringPlugin, string missingDependency, string? suggestedPlugin = null)
        : base(BuildMessage(requiringPlugin, missingDependency, suggestedPlugin))
    {
        RequiringPlugin = requiringPlugin;
        MissingDependency = missingDependency;
        SuggestedPlugin = suggestedPlugin;
    }

    private static string BuildMessage(string requiring, string missing, string? suggested) => 
        suggested is null
            ? $"Plugin '{requiring}' requires '{missing}', which has not been registered. Add it before '{requiring}'."
            : $"Plugin '{requiring}' requires '{missing}'. Add '{suggested}' before '{requiring}'.";
}