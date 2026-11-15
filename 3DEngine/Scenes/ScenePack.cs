namespace Engine;

/// <summary>A well-known scene kept as a pack fetched on demand, as its manifest names it.</summary>
/// <remarks>
/// A detailed scene's files, even with its textures made small, are too large for a repository to
/// hold, so a manifest of JSON holding these fields is kept in the repository's <c>scenes</c>
/// folder in its place, written as BevyCSharp's are. Its fields are settable, since a
/// source-generated reader gives a field a manifest leaves out the value it starts with.
/// </remarks>
public sealed record ScenePack
{
    /// <summary>What it is called, which its manifest's file and its folder are named by.</summary>
    public string Name { get; set; } = "";

    /// <summary>What a person is shown it as.</summary>
    public string Title { get; set; } = "";

    /// <summary>Where the scene was published by its makers.</summary>
    public string Source { get; set; } = "";

    /// <summary>The license it is published under.</summary>
    public string License { get; set; } = "";

    /// <summary>The words its license asks to be shown with it.</summary>
    public string Attribution { get; set; } = "";

    /// <summary>Where the pack is fetched from, over HTTPS or as a file on this machine.</summary>
    public string Url { get; set; } = "";

    /// <summary>How many bytes the pack is.</summary>
    public long Size { get; set; }

    /// <summary>The pack's SHA-256 in lowercase hexadecimal, which a fetched copy must have.</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>The model to load from the pack's files, by its path among them, such as <c>scene.gltf</c>.</summary>
    public string Model { get; set; } = "";
}
