namespace Engine;

/// <summary>
/// Backend-agnostic reader interface that converts a stream / file of a particular format
/// into an engine <see cref="Scene"/> snapshot, as <see cref="AssimpModelReader"/> does for
/// model files.
/// </summary>
/// <remarks>
/// <para>
/// Per the project's threading model, <see cref="ReadAsync"/> is called from an
/// <see cref="AssetServer"/> background worker. Implementations must publish only immutable
/// data in the returned <see cref="Scene"/>, with no native handles in the snapshot.
/// </para>
/// <para>
/// <b>Coordinate / unit policy:</b> readers <i>preserve</i> the source basis and units
/// (see <see cref="Scene.SourceCoordinateSystem"/> / <see cref="Scene.SourceMetersPerUnit"/>)
/// rather than rotating / rescaling vertex data on load. Spawn systems apply a single
/// root-level basis-change matrix derived from those fields. This keeps a
/// <c>read → write</c> round-trip byte-stable and avoids per-vertex precision loss on
/// large stages.
/// </para>
/// </remarks>
public interface ISceneReader
{
    /// <summary>
    /// File extensions this reader handles, including the leading dot
    /// (e.g. <c>[".gltf", ".glb"]</c>).
    /// </summary>
    string[] Extensions { get; }

    /// <summary>Identifier used by <see cref="SceneAsset.SourceFormat"/> (e.g. <c>"assimp"</c>).</summary>
    string FormatId { get; }

    /// <summary>
    /// Reads a scene from <paramref name="context"/>. Called on a background thread.
    /// </summary>
    Task<Scene> ReadAsync(AssetLoadContext context, SceneImportSettings settings, CancellationToken ct);
}

/// <summary>
/// Backend-agnostic writer interface for serializing a <see cref="Scene"/> back to a source format.
/// The engine has none, and a format that is written registers one.
/// </summary>
public interface ISceneWriter
{
    /// <summary>Identifier matching <see cref="SceneAsset.SourceFormat"/> .</summary>
    string FormatId { get; }

    /// <summary>Writes <paramref name="scene"/> to <paramref name="targetPath"/> in this writer's format.</summary>
    Task WriteAsync(Scene scene, string targetPath, SceneExportSettings settings, CancellationToken ct);
}

/// <summary>
/// Settings forwarded to an <see cref="ISceneReader"/>. Defaults match the runtime spawn
/// path (render purposes, all payloads, every material), and a load may override them.
/// </summary>
public sealed class SceneImportSettings
{
    /// <summary>Engine canonical coordinate system. Recorded for downstream basis-change.</summary>
    public SceneCoordinateSystem TargetCoordinateSystem { get; init; } = SceneCoordinateSystem.YUp;

    /// <summary>Engine canonical scale (meters per unit). Recorded for downstream scaling.</summary>
    public double TargetMetersPerUnit { get; init; } = 1.0;

    /// <summary>
    /// When <c>true</c>, the reader resolves references / payloads / variant selections
    /// and produces a flattened snapshot. Always <c>true</c> for runtime; the editor may
    /// also keep a live native handle alongside (owned outside <see cref="SceneAsset"/>).
    /// </summary>
    public bool FlattenComposition { get; init; } = true;

    /// <summary>
    /// Authoring purposes the reader should include. Nodes whose
    /// <see cref="SceneNode.Purpose"/> is not in this mask are skipped (or attached
    /// disabled, at the reader's discretion). Defaults to
    /// <see cref="ScenePurposeMask.Runtime"/> (default + render).
    /// </summary>
    public ScenePurposeMask IncludePurposes { get; init; } = ScenePurposeMask.Runtime;

    /// <summary>
    /// The time at which a reader of a format with animated attributes evaluates them, which
    /// other readers ignore. <c>null</c> for the file's own default time.
    /// </summary>
    public double? TimeCode { get; init; }

    /// <summary>
    /// How aggressively the reader should resolve material networks. Defaults to
    /// <see cref="MaterialNetworkResolution.Pbr"/>; pass
    /// <see cref="MaterialNetworkResolution.None"/> for geometry-only loads.
    /// </summary>
    public MaterialNetworkResolution MaterialResolution { get; init; } = MaterialNetworkResolution.Pbr;

    /// <summary>
    /// Which payload kinds to populate on <see cref="SceneNode.Components"/>. Defaults
    /// to <see cref="LoadPayloads.All"/>; set to a narrower mask to skip work the caller
    /// will not consume (e.g. thumbnails want meshes only).
    /// </summary>
    public LoadPayloads LoadPayloads { get; init; } = LoadPayloads.All;

    /// <summary>
    /// Further folders the reader looks in for files a model names by a relative path. Empty
    /// by default, when the reader looks in the source file's own folder alone.
    /// </summary>
    public IReadOnlyList<string> AssetSearchPath { get; init; } = Array.Empty<string>();

    /// <summary>Reusable default settings.</summary>
    public static SceneImportSettings Default { get; } = new();
}

/// <summary>Settings forwarded to an <see cref="ISceneWriter"/>.</summary>
public sealed class SceneExportSettings
{
    /// <summary>Format-native coordinate system the writer should emit (defaults to engine canonical).</summary>
    public SceneCoordinateSystem CoordinateSystem { get; init; } = SceneCoordinateSystem.YUp;

    /// <summary>Format-native unit scale the writer should emit.</summary>
    public double MetersPerUnit { get; init; } = 1.0;

    /// <summary>
    /// Hint to the writer that the produced asset should embed referenced textures into a
    /// single self-contained package. The writer infers the container from the target file's
    /// extension, and this flag says only whether the textures it names go inside it.
    /// Defaults to <c>false</c>, when texture files are referenced by path.
    /// </summary>
    public bool EmbedTextures { get; init; } = false;

    /// <summary>Reusable default settings.</summary>
    public static SceneExportSettings Default { get; } = new();
}

/// <summary>
/// Resource that holds the set of <see cref="ISceneReader"/> / <see cref="ISceneWriter"/>
/// implementations plugins register, which spawn systems pick from by format or extension
/// without naming a reader.
/// </summary>
/// <remarks>
/// Inserted into the <see cref="World"/> by <see cref="ScenesPlugin"/>. A plugin with a
/// reader or writer, as <see cref="AssimpModelPlugin"/> has, calls <see cref="RegisterReader"/> or
/// <see cref="RegisterWriter"/> during <see cref="IPlugin.Build"/>.
/// </remarks>
public sealed class SceneReaderRegistry
{
    private readonly Dictionary<string, ISceneReader> _readersByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISceneReader> _readersByFormat = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISceneWriter> _writersByFormat = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a reader for all of its declared extensions and its format id.</summary>
    public void RegisterReader(ISceneReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _readersByFormat[reader.FormatId] = reader;
        foreach (var ext in reader.Extensions)
            _readersByExtension[ext] = reader;
    }

    /// <summary>Registers a writer for its format id.</summary>
    public void RegisterWriter(ISceneWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writersByFormat[writer.FormatId] = writer;
    }

    /// <summary>Looks up a reader by file extension (e.g. <c>".usda"</c>).</summary>
    public ISceneReader? FindReaderByExtension(string extension)
        => _readersByExtension.TryGetValue(extension, out var r) ? r : null;

    /// <summary>Looks up a reader by format id (e.g. <c>"usd"</c>).</summary>
    public ISceneReader? FindReaderByFormat(string formatId)
        => _readersByFormat.TryGetValue(formatId, out var r) ? r : null;

    /// <summary>Looks up a writer by format id (e.g. <c>"usd"</c>).</summary>
    public ISceneWriter? FindWriterByFormat(string formatId)
        => _writersByFormat.TryGetValue(formatId, out var w) ? w : null;
}