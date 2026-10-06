using System.Numerics;

namespace Engine;

/// <summary>
/// Walks an entity's character body (made with <see cref="PhysicsWorld.CreateCharacter"/> and kept
/// on the entity as its <see cref="PhysicsBody"/>): the physics step reads the wanted velocity,
/// steepest slope, jump, step height and height from it before stepping, and writes back whether
/// it stands on ground.
/// </summary>
/// <example>
/// <code>
/// var player = ecs.Spawn();
/// ecs.Add(player, physics.CreateCharacter(feet, 0.4f, 1.8f, entityId: player));
/// ecs.Add(player, CharacterController.Default);
/// // In a behavior, each frame:
/// ref var controller = ref ctx.Ecs.GetRef&lt;CharacterController&gt;(ctx.EntityId);
/// controller.Velocity = input * 4;
/// if (jumpPressed &amp;&amp; controller.Grounded) controller.Jump = 5;
/// </code>
/// </example>
[SceneComponent]
public struct CharacterController
{
    /// <summary>The velocity it walks at along the ground. The vertical part is ignored.</summary>
    public Vector3 Velocity;

    /// <summary>The steepest ground, in degrees, it stands and walks on.</summary>
    public float MaxSlope;

    /// <summary>A jump's speed, taken at the next step if it stands on ground, and set back to 0.</summary>
    public float Jump;

    /// <summary>The highest step, in units, it climbs onto as it walks into it, or 0 to leave it as it is, its radius to begin with.</summary>
    public float StepHeight;

    /// <summary>
    /// How tall it stands, in units, with its feet where they are, as crouching and standing set it,
    /// or 0 to leave it as it was made. A height it has no room for overhead is taken once there is.
    /// </summary>
    public float Height;

    /// <summary>Whether it stood on ground at the last step. Written by the physics step.</summary>
    public bool Grounded;

    /// <summary>Standing still, with a 45 degree slope limit.</summary>
    public static CharacterController Default => new() { MaxSlope = 45 };
}
