using System.Numerics;

namespace Engine;

public static partial class E3D
{
    /// <summary>
    /// Says that every place the program hands the engine moved by <paramref name="offset"/>, a
    /// whole number of units along each axis, as a game far from the world's origin moves its own
    /// world back near it, so the engine moves what it keeps by place with it and the frame after
    /// the move draws, lights and simulates as the frame before did.
    /// </summary>
    /// <param name="offset">The step every place moved by, a whole number of units along each axis.</param>
    /// <remarks>
    /// <para>
    /// Called once in the frame the world moves, before anything is drawn, after the program has
    /// moved or as it moves its own places: each mesh entity's <see cref="Transform"/> and the
    /// matrices it draws through, its cameras, lights and emitters, all by the same step. The
    /// engine moves what it keeps: the scene's distance field and the meshes it knows by their
    /// matrices, so a mesh drawn at its matrix moved by the step is the same still mesh and none is
    /// built again, global illumination's probes and the light they gathered, the frames before
    /// the passes read their history from, the shadows' texels, the reflection probes and what
    /// they captured, the particles alive in the world's space, the poses of the bodies the physics
    /// simulates, which write their entities' transforms, and the places of the sounds playing. A
    /// mesh known by its matrix is any the frame draws, a mesh entity's, one drawn by
    /// <see cref="DrawMesh"/> and each of <see cref="DrawMeshInstanced"/>'s alike, and it must be
    /// handed next at its matrix moved by the step to the bit, which a whole
    /// step added to a place of a float keeps where the place is not far out, so the step is a
    /// whole number of units, and a step that is not is refused. Any whole step is taken, a
    /// teleport's of thousands of units as well as a step back near the origin.
    /// </para>
    /// <para>
    /// The bodies and the sounds move in the call itself, so a program that sets its sounds' or
    /// bodies' places itself in the frame of the move calls this first and then sets them where
    /// they are after the step.
    /// </para>
    /// <para>
    /// The distance field's cells keep what they hold where the step is a whole count of every
    /// cascade's cell, as a step of a power of two is of cells of powers of two; a cascade whose
    /// cell the step is not a whole count of is built again over the frames after.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The step is not a whole number of units along an axis.</exception>
    public static void MoveWorld(Vector3 offset)
    {
        if (!float.IsInteger(offset.X) || !float.IsInteger(offset.Y) || !float.IsInteger(offset.Z))
            throw new ArgumentException($"MoveWorld: the step {offset} is not a whole number of units along each axis, which a place moved by it needs to be the same float the program hands the engine next.", nameof(offset));
        var step = new WorldStep(offset.X, offset.Y, offset.Z);
        if (step.IsZero) return;
        World.GetOrInsertResource(static () => new WorldMove()).Add(step);
        if (TryRes<PhysicsWorld>(out var physics)) physics.Move(offset);
        if (TryRes<AudioServer>(out var audio)) audio.Move(offset);
    }
}
