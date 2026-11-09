namespace Engine;

/// <summary>
/// Authoring-time visibility classification for scene nodes, mirroring the four
/// purposes defined by USD's <c>UsdGeomImageable.purpose</c> attribute.
/// </summary>
/// <remarks>
/// <para>
/// USD splits a stage into "purposes", so one file can carry the model a game ships and the
/// modeler's proxy boxes and guides. The spawn system chooses which purposes it spawns, and a
/// game takes <see cref="Default"/> and <see cref="Render"/>. A reader of a format with no
/// purposes, as <see cref="AssimpModelReader"/>, gives every node <see cref="Default"/>.
/// </para>
/// </remarks>
public enum ScenePurpose
{
    /// <summary>No purpose was authored, so the node takes part in every render pass.</summary>
    Default = 0,

    /// <summary>Final-quality geometry meant for the beauty render.</summary>
    Render = 1,

    /// <summary>Lightweight stand-in geometry for fast viewport / interaction.</summary>
    Proxy = 2,

    /// <summary>Non-shippable construction / debug geometry (curves, locators, ...).</summary>
    Guide = 3,
}

/// <summary>
/// Selects how aggressively an <see cref="ISceneReader"/> should resolve a source
/// material network into the engine's <see cref="SceneMaterialPayload"/>.
/// </summary>
/// <remarks>
/// The default <see cref="Pbr"/> mode reads the shading attributes that map one to one onto
/// the engine's metallic-roughness payload, which is the glTF model Assimp maps every format
/// onto.
/// </remarks>
internal enum MaterialNetworkResolution
{
    /// <summary>Skip materials entirely (geometry-only loads).</summary>
    None,

    /// <summary>Resolve the metallic-roughness subset (default).</summary>
    Pbr,
}

/// <summary>
/// Bitmask of payload kinds an <see cref="ISceneReader"/> should populate on
/// <see cref="SceneNode.Components"/>. Lets callers skip categories they do not need
/// (e.g. a thumbnail importer can request meshes only).
/// </summary>
[Flags]
internal enum LoadPayloads
{
    /// <summary>Load no payloads (hierarchy + transforms only).</summary>
    None = 0,

    /// <summary>Load <see cref="SceneMeshPayload"/> components.</summary>
    Meshes = 1 << 0,

    /// <summary>Load <see cref="SceneMaterialPayload"/> components and material bindings.</summary>
    Materials = 1 << 1,

    /// <summary>Load <see cref="SceneCameraPayload"/> components.</summary>
    Cameras = 1 << 2,

    /// <summary>Load <see cref="SceneLightPayload"/> components.</summary>
    Lights = 1 << 3,

    /// <summary>Load <see cref="SceneInstancingPayload"/> components.</summary>
    Instancing = 1 << 4,

    /// <summary>All currently-defined payload kinds (default).</summary>
    All = Meshes | Materials | Cameras | Lights | Instancing,
}
