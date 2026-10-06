namespace Engine;

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
