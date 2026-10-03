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
                _accumulator -= _settings.FixedTimeStep;
                steps++;
            }

            if (steps == _settings.MaxStepsPerFrame)
                _accumulator = 0f; // avoid spiral of death
        }
        else
        {
            Simulation.Timestep(deltaSeconds, Dispatcher);
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
            ref var t = ref ecs.GetRef<Transform>(entity);
            if (alpha < 1f && _previousPoses.TryGetValue(handleValue, out var before))
            {
                t.Position = Vector3.Lerp(before.Position, br.Pose.Position, alpha);
                t.Rotation = Quaternion.Slerp(before.Orientation, br.Pose.Orientation, alpha);
            }
            else
            {
                t.Position = br.Pose.Position;
                t.Rotation = br.Pose.Orientation;
            }
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