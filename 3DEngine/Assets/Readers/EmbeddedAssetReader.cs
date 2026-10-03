using System.Reflection;

namespace Engine;

/// <summary>
/// <see cref="IAssetReader"/> backed by assembly embedded resources.
/// Useful for shipping built-in/default assets inside a DLL.
/// </summary>
/// <remarks>
/// <para>
/// Embedded resource names follow the CLR convention of dot-separated namespace paths.
/// This reader maps <see cref="AssetPath"/> forward-slash paths to the dot convention
/// using a configurable prefix (e.g. <c>"Engine.Assets"</c>).
/// </para>
/// <para>Does not support file watching (always returns <c>null</c> from <see cref="CreateWatcher"/>).</para>
/// </remarks>
/// <example>
/// <code>
/// // Assembly contains "MyGame.Assets.textures.default.png" as embedded resource
/// var reader = new EmbeddedAssetReader(typeof(MyGame).Assembly, "MyGame.Assets");
/// var stream = await reader.ReadAsync(new AssetPath("textures/default.png"));
/// </code>
/// </example>
/// <seealso cref="IAssetReader"/>
/// <seealso cref="FileAssetReader"/>
public sealed class EmbeddedAssetReader : IAssetReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Assets.Embedded");

    private readonly Assembly _assembly;
    private readonly string _prefix;
    private readonly HashSet<string> _resourceNames;

    /// <summary>
    /// Creates a new <see cref="EmbeddedAssetReader"/>.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resources.</param>
    /// <param name="prefix">
    /// The dot-separated resource name prefix, e.g. <c>"Engine.Assets"</c>.
    /// Asset paths are appended after this prefix.
    /// </param>
    public EmbeddedAssetReader(Assembly assembly, string prefix)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _prefix = prefix.TrimEnd('.');
        _resourceNames = new HashSet<string>(assembly.GetManifestResourceNames(), StringComparer.OrdinalIgnoreCase);
        Logger.Debug($"EmbeddedAssetReader: {_assembly.GetName().Name}, prefix='{_prefix}', {_resourceNames.Count} resources");
    }

    /// <inheritdoc />
    public bool Exists(AssetPath path) => _resourceNames.Contains(ToResourceName(path));

    /// <inheritdoc />
    public Task<Stream> ReadAsync(AssetPath path, CancellationToken ct = default)
    {
        string name = ToResourceName(path);
        var stream = _assembly.GetManifestResourceStream(name);
        if (stream is null)
            throw new FileNotFoundException($"Embedded resource not found: {name} (path: {path})", name);

        Logger.Debug($"Reading embedded resource: {name}");
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public IAssetWatcher? CreateWatcher() => null; // Embedded resources don't change at runtime

    private string ToResourceName(AssetPath path) =>
        $"{_prefix}.{path.Path.Replace('/', '.').Replace('\\', '.')}";
}
