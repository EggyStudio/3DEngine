namespace Engine;

/// <summary>
/// Bitmask used by <see cref="SceneImportSettings.IncludePurposes"/> (and downstream
/// spawn filters) to select which <see cref="ScenePurpose"/> values to materialize.
/// </summary>
[Flags]
public enum ScenePurposeMask
{
    /// <summary>Include nothing (used for explicit clear).</summary>
    None = 0,

    /// <summary>Include nodes authored with no explicit purpose.</summary>
    Default = 1 << 0,

    /// <summary>Include final-render geometry.</summary>
    Render = 1 << 1,

    /// <summary>Include proxy / preview geometry.</summary>
    Proxy = 1 << 2,

    /// <summary>Include construction / debug geometry.</summary>
    Guide = 1 << 3,

    /// <summary>Runtime default: <see cref="Default"/> + <see cref="Render"/>.</summary>
    Runtime = Default | Render,

    /// <summary>What a tool that shows proxies includes: <see cref="Default"/>, <see cref="Render"/> and <see cref="Proxy"/>.</summary>
    Editor = Default | Render | Proxy,

    /// <summary>Every authored purpose (including guides).</summary>
    All = Default | Render | Proxy | Guide,
}
