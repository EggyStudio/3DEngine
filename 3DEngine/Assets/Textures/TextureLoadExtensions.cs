namespace Engine;

/// <summary>
/// Convenience helpers that collapse the standard "load a <see cref="Texture"/> through
/// the <see cref="AssetServer"/>" boilerplate into single calls. Mirrors
/// <see cref="SceneSpawnExtensions"/>: the lower-level building blocks remain available
/// for callers who need fine-grained control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Color-space hints</b> are forwarded to <see cref="TextureAssetLoader"/> via the
/// sub-asset label channel: <c>"srgb"</c> for base-color / emissive maps, <c>"linear"</c>
/// for normal / metallic-roughness / occlusion maps. Combine with <c>"mips"</c> to
/// request a generated mip chain (e.g. <c>"srgb|mips"</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default: linear color-space, no mips - decoder defaults.
/// Handle&lt;Texture&gt; tex = ctx.LoadTexture("textures/wood.png");
/// </code>
/// <code>
/// // Albedo / base-color - sRGB, mip chain generated up-front:
/// Handle&lt;Texture&gt; albedo = ctx.LoadTextureSrgb("textures/wood_albedo.png", generateMips: true);
/// </code>
/// <code>
/// // Normal map - linear, no mips:
/// Handle&lt;Texture&gt; normal = ctx.LoadTextureLinear("textures/wood_normal.png");
/// </code>
/// </example>
/// <seealso cref="Texture"/>
/// <seealso cref="TextureAssetLoader"/>
/// <seealso cref="SceneSpawnExtensions"/>
public static class TextureLoadExtensions
{
    /// <summary>Loads a <see cref="Texture"/> via the <see cref="AssetServer"/> with no overrides.</summary>
    public static Handle<Texture> LoadTexture(this AssetServer server, string path) =>
        server.Load<Texture>(path);

    /// <summary>Loads a <see cref="Texture"/> through the world's <see cref="AssetServer"/>.</summary>
    public static Handle<Texture> LoadTexture(this World world, string path) =>
        world.Resource<AssetServer>().Load<Texture>(path);

    /// <summary>Loads a <see cref="Texture"/> through the behavior context's world.</summary>
    public static Handle<Texture> LoadTexture(this BehaviorContext ctx, string path) =>
        ctx.World.Resource<AssetServer>().Load<Texture>(path);

    // -- sRGB convenience (BaseColor / Emissive) --

    /// <summary>Loads as sRGB-encoded; pass <paramref name="generateMips"/> = <c>true</c> for a full chain.</summary>
    public static Handle<Texture> LoadTextureSrgb(this AssetServer server, string path, bool generateMips = false) =>
        server.Load<Texture>(BuildLabelledPath(path, srgb: true, mips: generateMips));

    /// <inheritdoc cref="LoadTextureSrgb(AssetServer, string, bool)"/>
    public static Handle<Texture> LoadTextureSrgb(this World world, string path, bool generateMips = false) =>
        world.Resource<AssetServer>().Load<Texture>(BuildLabelledPath(path, srgb: true, mips: generateMips));

    /// <inheritdoc cref="LoadTextureSrgb(AssetServer, string, bool)"/>
    public static Handle<Texture> LoadTextureSrgb(this BehaviorContext ctx, string path, bool generateMips = false) =>
        ctx.World.Resource<AssetServer>().Load<Texture>(BuildLabelledPath(path, srgb: true, mips: generateMips));

    // -- Linear convenience (Normal / MR / Occlusion / data) --

    /// <summary>Loads as linear; pass <paramref name="generateMips"/> = <c>true</c> for a full chain.</summary>
    public static Handle<Texture> LoadTextureLinear(this AssetServer server, string path, bool generateMips = false) =>
        server.Load<Texture>(BuildLabelledPath(path, srgb: false, mips: generateMips));

    /// <inheritdoc cref="LoadTextureLinear(AssetServer, string, bool)"/>
    public static Handle<Texture> LoadTextureLinear(this World world, string path, bool generateMips = false) =>
        world.Resource<AssetServer>().Load<Texture>(BuildLabelledPath(path, srgb: false, mips: generateMips));

    /// <inheritdoc cref="LoadTextureLinear(AssetServer, string, bool)"/>
    public static Handle<Texture> LoadTextureLinear(this BehaviorContext ctx, string path, bool generateMips = false) =>
        ctx.World.Resource<AssetServer>().Load<Texture>(BuildLabelledPath(path, srgb: false, mips: generateMips));

    private static string BuildLabelledPath(string path, bool srgb, bool mips)
    {
        var token = (srgb, mips) switch
        {
            (true,  true)  => "srgb|mips",
            (true,  false) => "srgb",
            (false, true)  => "linear|mips",
            (false, false) => "linear",
        };
        // AssetPath.Parse handles any embedded '#' on the caller-supplied path by leaving
        // a single label after the first '#'; if the caller already added a label, our
        // token replaces it (matches the documented "last-write" semantics for labels).
        var idx = path.IndexOf('#');
        var basePath = idx < 0 ? path : path[..idx];
        return $"{basePath}#{token}";
    }
}