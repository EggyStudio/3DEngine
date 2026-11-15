namespace Engine;

/// <summary>The stage a fetch of a scene pack is at, which <see cref="ScenePackProgress"/> tells.</summary>
public enum ScenePackStage
{
    /// <summary>The pack's bytes are coming, hashed as they come.</summary>
    Downloading,

    /// <summary>The pack's size and SHA-256 are held against its manifest's.</summary>
    Checking,

    /// <summary>The pack's files are written into a folder of their own.</summary>
    Unpacking,

    /// <summary>The pack is fetched, checked and unpacked, or was before the fetch began.</summary>
    Done,
}
