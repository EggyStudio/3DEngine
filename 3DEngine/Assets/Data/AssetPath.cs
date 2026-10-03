namespace Engine;

/// <summary>
/// Lightweight path representation for assets. Combines a relative file path with an
/// optional label for sub-assets (e.g. <c>"models/tree.gltf#Mesh0"</c>).
/// </summary>
/// <remarks>
/// <para>
/// The path is always normalized to use forward slashes and is case-sensitive on disk.
/// Labels are separated by <c>#</c> and identify sub-assets within a compound file (e.g. GLTF scenes,
/// meshes, materials from a single file).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var path = new AssetPath("textures/ground.png");
/// var sub  = new AssetPath("models/tree.gltf", "Mesh0");
/// var parsed = AssetPath.Parse("models/tree.gltf#Mesh0");
/// </code>
/// </example>
/// <seealso cref="AssetServer"/>
/// <seealso cref="IAssetReader"/>
public readonly record struct AssetPath
{
    /// <summary>Relative path to the asset file (forward slashes, no leading slash).</summary>
    public string Path { get; }

    /// <summary>
    /// Optional label identifying a sub-asset within the file.
    /// <c>null</c> for simple single-asset files. E.g. <c>"Mesh0"</c>, <c>"Scene0"</c>.
    /// </summary>
    public string? Label { get; }

    /// <summary>Returns <c>true</c> when a sub-asset label is present.</summary>
    public bool HasLabel => Label is not null;

    /// <summary>The file extension including the leading dot, or empty if none. E.g. <c>".png"</c>, <c>".gltf"</c>.</summary>
    public string Extension => System.IO.Path.GetExtension(Path);

    /// <summary>The file name without directory, e.g. <c>"ground.png"</c>.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Creates an asset path from a relative file path and optional label.</summary>
    /// <param name="path">Relative path to the asset file.</param>
    /// <param name="label">Optional sub-asset label.</param>
    public AssetPath(string path, string? label = null)
    {
        Path = Normalize(path);
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    /// <summary>
    /// Parses a combined path string of the form <c>"path/to/file.ext"</c> or
    /// <c>"path/to/file.ext#Label"</c>.
    /// </summary>
    /// <param name="combined">The combined path string.</param>
    /// <returns>A new <see cref="AssetPath"/> with the parsed path and optional label.</returns>
    public static AssetPath Parse(string combined)
    {
        ArgumentNullException.ThrowIfNull(combined);
        int hash = combined.IndexOf('#');
        if (hash < 0)
            return new AssetPath(combined);
        return new AssetPath(combined[..hash], combined[(hash + 1)..]);
    }

    /// <summary>Returns the canonical string form: <c>"path/to/file.ext"</c> or <c>"path/to/file.ext#Label"</c>.</summary>
    public override string ToString() => Label is null ? Path : $"{Path}#{Label}";

    /// <summary>Returns this path without the label (file-level only).</summary>
    public AssetPath WithoutLabel() => new(Path);

    /// <summary>Returns this path with a different label.</summary>
    /// <param name="label">The new sub-asset label.</param>
    public AssetPath WithLabel(string label) => new(Path, label);

    private static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/').TrimStart('/');
    }
}
