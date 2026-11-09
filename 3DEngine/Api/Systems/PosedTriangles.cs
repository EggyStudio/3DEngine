using System.Numerics;

namespace Engine;

/// <summary>The animated models' posed triangles read from their draws and the mesh store, for physics.</summary>
internal sealed class PosedTriangles(World world) : IPosedTriangles
{
    public bool TryPosedTriangles(EcsWorld ecs, int entity, out Vector3[] triangles, out bool never)
    {
        (triangles, never) = ([], false);
        return world.TryGetResource<AnimatedModelDraws>(out var animated) && world.TryGetResource<MeshStore>(out var meshes)
            && animated.TryPosedTriangles(ecs, meshes, entity, out triangles, out never);
    }
}
