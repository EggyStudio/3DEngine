namespace Engine;

/// <summary>What an <see cref="EditMesh"/> holds, its counts, its selection, its volume and its bounds, as <see cref="E3D.GetEditMeshInfo"/> reads them.</summary>
/// <param name="VertexCount">How many vertices it has.</param>
/// <param name="EdgeCount">How many edges its faces have between them, an edge two faces share counted once.</param>
/// <param name="FaceCount">How many faces it has.</param>
/// <param name="OpenEdges">
/// How many edges are not shared by two faces running along them in opposite directions, an edge
/// of a hole, an edge three faces meet at, or one beside a face turned the wrong way.
/// </param>
/// <param name="SelectedVertexCount">How many vertices are selected, the corners of the faces selected among them.</param>
/// <param name="SelectedEdgeCount">How many edges are selected, which is those whose two ends are.</param>
/// <param name="SelectedFaceCount">How many faces are selected.</param>
/// <param name="Volume">
/// The space a closed mesh holds, in cubic meters, negative where its faces face in. A mesh with
/// open edges has no inside, and the number means nothing.
/// </param>
/// <param name="Bounds">The smallest box holding every vertex, or two zeros for a mesh with none.</param>
/// <param name="GridStep">The grid step in meters that <see cref="E3D.EditMeshSnap"/> rounds to.</param>
public readonly record struct EditMeshInfo(
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int OpenEdges,
    int SelectedVertexCount,
    int SelectedEdgeCount,
    int SelectedFaceCount,
    float Volume,
    BoundingBox Bounds,
    float GridStep)
{
    /// <summary>Whether the mesh has a face and every edge is shared by two faces running along it in opposite directions.</summary>
    /// <remarks>A collider of a mesh that is not closed has holes, and its <see cref="Volume"/> means nothing.</remarks>
    public bool IsClosed => FaceCount > 0 && OpenEdges == 0;
}
