namespace Engine;

/// <summary>The physics world a behavior reaches through its <see cref="BehaviorContext"/>.</summary>
public static class BehaviorContextPhysics
{
    extension(BehaviorContext context)
    {
        /// <summary>Active physics world (rigid bodies, raycasts, gravity).</summary>
        /// <remarks>
        /// Resolved when read rather than when the context is made, so an app without
        /// <c>PhysicsPlugin</c> runs every behavior that does not touch physics.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The world has no <see cref="PhysicsWorld"/>.</exception>
        public PhysicsWorld Physics => context.World.Resource<PhysicsWorld>();
    }
}
