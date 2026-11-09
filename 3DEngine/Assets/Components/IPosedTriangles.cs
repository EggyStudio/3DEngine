using System.Numerics;

namespace Engine;

/// <summary>
/// The triangles of the model an entity's <see cref="AnimatedModel"/> draws, as it was last posed, which
/// physics makes a mesh or a hull collider of without naming what draws the model.
/// </summary>
internal interface IPosedTriangles
{
    /// <summary>
    /// The posed model's triangles in its own space, three corners each, and whether its file gives
    /// none, so a collider waiting for them never gets them.
    /// </summary>
    bool TryPosedTriangles(EcsWorld ecs, int entity, out Vector3[] triangles, out bool never);
}
