using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>The bodies moved with a program's world, every place it hands the engine moved by a whole step.</summary>
public sealed partial class PhysicsWorld
{
    /// <summary>
    /// Moves every body by a whole step a program moved its world by, dynamic, kinematic and static,
    /// and each body's pose before the last step with it, so a body drawn between two steps is
    /// blended from where it was, and its velocity and what it rests on kept.
    /// </summary>
    internal void Move(Vector3 step)
    {
        if (step == Vector3.Zero) return;
        // Every body of every set, awake or asleep, and every static, whether an entity holds it or not.
        var bodies = Simulation.Bodies;
        for (int set = 0; set < bodies.Sets.Length; set++)
        {
            ref var bodySet = ref bodies.Sets[set];
            if (!bodySet.Allocated) continue;
            for (int i = 0; i < bodySet.Count; i++)
            {
                var body = bodies.GetBodyReference(bodySet.IndexToHandle[i]);
                body.Pose.Position += step;
                body.UpdateBounds();
            }
        }
        var statics = Simulation.Statics;
        for (int i = 0; i < statics.Count; i++)
        {
            var handle = statics.IndexToHandle[i];
            statics.GetStaticReference(handle).Pose.Position += step;
            statics.UpdateBounds(handle);
        }
        foreach (var handleValue in _previousPoses.Keys.ToArray())
        {
            var (position, orientation) = _previousPoses[handleValue];
            _previousPoses[handleValue] = (position + step, orientation);
        }
    }
}
