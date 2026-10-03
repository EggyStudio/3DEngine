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
    public void SyncTransforms(EcsWorld ecs)
    {
        // Walk every dynamic / kinematic body and write its pose into the matching Transform component.
        var bodies = Simulation.Bodies;
        foreach (var (handleValue, entity) in _bodyToEntity)
        {
            var loc = bodies.HandleToLocation[handleValue];
            if (loc.SetIndex < 0) continue;
            var br = bodies.GetBodyReference(new BodyHandle(handleValue));
            if (!ecs.Has<Transform>(entity)) continue;
            ref var t = ref ecs.GetRef<Transform>(entity);
            t.Position = br.Pose.Position;
            t.Rotation = br.Pose.Orientation;
        }
    }
}