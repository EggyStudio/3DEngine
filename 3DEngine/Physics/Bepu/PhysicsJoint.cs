using System.Numerics;
using BepuPhysics;
using BepuPhysics.Constraints;

namespace Engine;

/// <summary>A constraint holding two bodies together, by its handle in the solver.</summary>
/// <param name="Handle">The solver's handle, or -1 for none.</param>
public readonly record struct PhysicsJoint(int Handle)
{
    /// <summary>No joint.</summary>
    public static PhysicsJoint None => new(-1);

    /// <summary>Whether this names a joint that was made.</summary>
    public bool IsValid => Handle >= 0;
}
