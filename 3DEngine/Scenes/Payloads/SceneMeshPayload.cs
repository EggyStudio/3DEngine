using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic mesh payload attached to a <see cref="SceneNode"/> via
/// <see cref="SceneNode.Components"/>. Produced by readers (e.g. <c>UsdSceneReader</c>) and
/// translated to renderer-side <see cref="Mesh"/> components by <c>SceneSpawnSystem</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Source-of-truth model:</b> this payload mirrors the data found in a
/// <c>UsdGeomMesh</c> after polygon triangulation. Fields are deliberately additive so the
/// same payload can describe an indexed PBR-ready mesh and a flat triangle soup; consumers
/// pick what they need based on the renderer's current capabilities.
/// </para>
/// <para>
/// <b>v1 spawn-system contract</b> (per the OpenUSD-as-source-of-truth ticket): the runtime
/// <see cref="Mesh"/> component still consumes a flat <c>Vector3[]</c>. Until the renderer
/// grows index-buffer + UV + normal support, <c>SceneSpawnSystem</c> de-indexes
/// <see cref="Indices"/> into a flat array of size <c>Indices.Length</c> at spawn time
/// (positions[i] = <see cref="Positions"/>[<see cref="Indices"/>[i]]). The richer fields
/// (<see cref="Normals"/>, <see cref="Tangents"/>, <see cref="Uv0"/>, ...) are carried
/// forward losslessly so a renderer-side upgrade can read them without re-parsing the source.
/// </para>
/// <para>
/// <b>Coordinate space:</b> positions, normals and tangents are in the source file's
/// authored basis and units. The reader does <i>not</i> normalize - <c>SceneSpawnSystem</c>
/// applies a single root-level basis/unit matrix derived from
/// <see cref="Scene.SourceCoordinateSystem"/> and <see cref="Scene.SourceMetersPerUnit"/>
/// instead, to keep the read/write round-trip byte-stable (cf. Plan §B).
/// </para>
/// <para>
/// <b>Threading:</b> instances are immutable by convention once attached to a published
/// <see cref="SceneAsset"/>; arrays are shared, never copied. Do not mutate them.
/// </para>
/// <para>
/// TODO: <c>SceneSkinningPayload</c> (joints/weights) and <c>SceneAnimationPayload</c>
/// (sampled time-varying attribute curves) ship as separate payloads in a follow-up ticket.
/// </para>
/// </remarks>
/// <seealso cref="SceneMeshSubset"/>
/// <seealso cref="SceneBounds"/>
/// <seealso cref="SceneMaterialPayload"/>
public sealed class SceneMeshPayload
{
    /// <summary>Display name (typically the source mesh prim's leaf name).</summary>
    public string Name { get; init; } = "Mesh";

    /// <summary>
    /// Vertex positions in the source file's authored basis/units. Required, never null.
    /// One entry per unique vertex; topology is expressed via <see cref="Indices"/>.
    /// </summary>
    public required Vector3[] Positions { get; init; }

    /// <summary>
    /// Triangle index buffer (length is always a multiple of 3). Required: the reader
    /// triangulates polygon meshes via <c>UsdGeomMesh.Triangulate</c> before constructing
    /// the payload, so consumers never have to deal with arbitrary-degree faces.
    /// </summary>
    public required int[] Indices { get; init; }

    /// <summary>
    /// Per-vertex normals aligned with <see cref="Positions"/>, or <c>null</c> when the
    /// source did not author them (consumer / renderer should derive flat normals from
    /// <see cref="Indices"/> in that case).
    /// </summary>
    public Vector3[]? Normals { get; init; }

    /// <summary>
    /// Per-vertex tangents aligned with <see cref="Positions"/> using the glTF / MikkTSpace
    /// convention: <c>xyz</c> is the tangent direction, <c>w</c> is the bitangent sign
    /// (±1) used to recover <c>bitangent = sign * cross(normal, tangent)</c>. <c>null</c>
    /// if not authored / not derivable.
    /// </summary>
    public Vector4[]? Tangents { get; init; }

    /// <summary>Primary UV channel (UsdPreviewSurface <c>primvars:st</c>). One entry per vertex, or <c>null</c>.</summary>
    public Vector2[]? Uv0 { get; init; }

    /// <summary>Secondary UV channel (e.g. <c>primvars:st1</c> for lightmaps). One entry per vertex, or <c>null</c>.</summary>
    public Vector2[]? Uv1 { get; init; }

    /// <summary>Per-vertex linear-RGBA colors (e.g. <c>primvars:displayColor</c> + <c>primvars:displayOpacity</c>), or <c>null</c>.</summary>
    public Vector4[]? Colors { get; init; }

    /// <summary>
    /// Material-binding subsets covering disjoint slices of <see cref="Indices"/>.
    /// An empty list means a single material covers the entire index range (whichever
    /// material is bound at the mesh prim level via <c>UsdShadeMaterialBindingAPI</c>).
    /// </summary>
    public IReadOnlyList<SceneMeshSubset> Subsets { get; init; } = Array.Empty<SceneMeshSubset>();

    /// <summary>
    /// Axis-aligned bounding box of <see cref="Positions"/>, computed once at read time
    /// in the source basis/units. Cheap to derive and useful for spawn-time culling, scene
    /// stats, and editor framing - so the consumer doesn't have to re-walk positions.
    /// </summary>
    public SceneBounds LocalBounds { get; init; } = SceneBounds.Empty;
}

/// <summary>
/// A face-vertex index range within a <see cref="SceneMeshPayload"/> bound to a single
/// material. Mirrors a <c>UsdGeomSubset</c> with <c>familyName = "materialBind"</c> after
/// post-triangulation index remapping.
/// </summary>
/// <param name="Name">Display name (subset prim's leaf name, or a synthesized default).</param>
/// <param name="IndexStart">Start offset into <see cref="SceneMeshPayload.Indices"/> (always a multiple of 3).</param>
/// <param name="IndexCount">Number of indices in this subset (always a multiple of 3).</param>
/// <param name="MaterialPath">
/// Source path of the bound material prim (e.g. <c>"/teapot/Looks/teapotMat"</c>),
/// matched against <see cref="SceneMaterialPayload.SourcePath"/> at spawn time.
/// May be <c>null</c> for "no material bound" subsets.
/// </param>
public sealed record SceneMeshSubset(string Name, int IndexStart, int IndexCount, string? MaterialPath);

/// <summary>
/// Axis-aligned bounding box in the same space as the owning payload's vertex data.
/// </summary>
/// <param name="Min">Component-wise minimum corner.</param>
/// <param name="Max">Component-wise maximum corner.</param>
public readonly record struct SceneBounds(Vector3 Min, Vector3 Max)
{
    /// <summary>An empty / inverted bounds value, used as the default for empty meshes.</summary>
    public static SceneBounds Empty { get; } = new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity));

    /// <summary><c>true</c> when <see cref="Min"/> is component-wise less than or equal to <see cref="Max"/>.</summary>
    public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;

    /// <summary>The center of the bounding box; only meaningful when <see cref="IsValid"/>.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>The full extents (Max - Min); only meaningful when <see cref="IsValid"/>.</summary>
    public Vector3 Size => Max - Min;

    /// <summary>Computes the AABB of <paramref name="positions"/> in a single pass, returning <see cref="Empty"/> if the span is empty.</summary>
    public static SceneBounds FromPositions(ReadOnlySpan<Vector3> positions)
    {
        if (positions.Length == 0) return Empty;
        var min = positions[0];
        var max = positions[0];
        for (int i = 1; i < positions.Length; i++)
        {
            min = Vector3.Min(min, positions[i]);
            max = Vector3.Max(max, positions[i]);
        }
        return new SceneBounds(min, max);
    }
}