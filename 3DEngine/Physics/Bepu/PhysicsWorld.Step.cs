using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>Per-frame stepping (fixed-timestep accumulator) and ECS transform write-back.</summary>
public sealed partial class PhysicsWorld
{
    /// <inheritdoc />
    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;
        if (_settings.UseFixedTimestep)
        {
            _accumulator += deltaSeconds;
            int steps = 0;
            while (_accumulator >= _settings.FixedTimeStep && steps < _settings.MaxStepsPerFrame)
            {
                Simulation.Timestep(_settings.FixedTimeStep, Dispatcher);
                UpdateContacts();
                _accumulator -= _settings.FixedTimeStep;
                steps++;
            }

            if (steps == _settings.MaxStepsPerFrame)
                _accumulator = 0f; // avoid spiral of death
        }
        else
        {
            Simulation.Timestep(deltaSeconds, Dispatcher);
            UpdateContacts();
        }
    }

    /// <inheritdoc />
    /// <summary>Advances the simulation by exactly one step of <paramref name="seconds"/>, with no accumulator.</summary>
    /// <remarks>What <see cref="Stage.FixedUpdate"/> calls, since the fixed stage has already done the accumulating.</remarks>
    public void StepOnce(float seconds)
    {
        if (seconds <= 0f) return;
        RememberPoses();
        Simulation.Timestep(seconds, Dispatcher);
        UpdateContacts();
    }

    /// <summary>Writes every body's pose into its entity's <see cref="Transform"/>, as it is.</summary>
    public void SyncTransforms(EcsWorld ecs) => SyncTransforms(ecs, alpha: 1f);

    /// <summary>
    /// Writes every body's pose into its entity's <see cref="Transform"/>, blended from its pose
    /// before the last step (at 0) to its pose after it (at 1).
    /// </summary>
    /// <remarks>
    /// Blending by <see cref="FixedTime.Alpha"/> draws a body where it was a fraction of a step ago,
    /// which is smooth however frames and steps line up. A body created since the last step, or
    /// moved with <see cref="SetPosition"/> or <see cref="SetRotation"/>, has no earlier pose to
    /// blend from and is written as it is.
    /// <para>
    /// A body's entity under a <see cref="Parent"/> is given the local transform that puts it at
    /// the body's pose under its parent as the parent is in this frame, its own scale kept, so the body stays
    /// where the simulation has it however the parent moves. A parent scaled unevenly and rotated
    /// cannot carry every rotation of a child, and its child then stands as near as a
    /// decomposition gets.
    /// </para>
    /// </remarks>
    public void SyncTransforms(EcsWorld ecs, float alpha)
    {
        var bodies = Simulation.Bodies;
        alpha = Math.Clamp(alpha, 0f, 1f);
        foreach (var (handleValue, entity) in _bodyToEntity)
        {
            var loc = bodies.HandleToLocation[handleValue];
            if (loc.SetIndex < 0) continue;
            var br = bodies.GetBodyReference(new BodyHandle(handleValue));
            if (!ecs.Has<Transform>(entity)) continue;
            // Read without marking, since a body at rest leaves its transform as it was, and a
            // transform marked every frame would have propagation recompute its chain each frame.
            ref readonly var t = ref ecs.GetReadOnly<Transform>(entity);
            Vector3 position;
            Quaternion rotation;
            if (alpha < 1f && _previousPoses.TryGetValue(handleValue, out var before))
            {
                position = Vector3.Lerp(before.Position, br.Pose.Position, alpha);
                rotation = Quaternion.Slerp(before.Orientation, br.Pose.Orientation, alpha);
            }
            else
            {
                position = br.Pose.Position;
                rotation = br.Pose.Orientation;
            }

            var parent = ecs.ParentOf(entity);
            if (parent != 0 && Matrix4x4.Invert(TransformPropagation.ComposedWorldMatrix(ecs, parent), out var toParent))
            {
                var world = Matrix4x4.CreateScale(t.Scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
                if (Matrix4x4.Decompose(world * toParent, out _, out var localRotation, out var localPosition))
                {
                    position = localPosition;
                    rotation = localRotation;
                }
            }

            if (position == t.Position && rotation == t.Rotation) continue;
            ref var written = ref ecs.GetRef<Transform>(entity);
            written.Position = position;
            written.Rotation = rotation;
        }
    }

    // The pose of every body as the step about to run finds it, which the next sync blends from.
    private void RememberPoses()
    {
        var bodies = Simulation.Bodies;
        foreach (var handleValue in _bodyToEntity.Keys)
        {
            if (bodies.HandleToLocation[handleValue].SetIndex < 0) continue;
            var pose = bodies.GetBodyReference(new BodyHandle(handleValue)).Pose;
            _previousPoses[handleValue] = (pose.Position, pose.Orientation);
        }
    }
}