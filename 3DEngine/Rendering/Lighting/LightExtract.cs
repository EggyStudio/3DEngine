using System.Numerics;

namespace Engine;

/// <summary>
/// Extracts entities with a <see cref="Light"/> component (placed by their <see cref="Transform"/>
/// when they have one) into render entities carrying
/// <see cref="RenderLight"/>, plus a flat <see cref="RenderLights"/> singleton on the
/// <see cref="RenderWorld"/>.
/// </summary>
/// <remarks>
/// The render-side per-frame buckets are cleared by <see cref="RenderWorld.ClearEntities"/>
/// before the extract runs; the singleton list is cleared here at the top of <see cref="Run"/>.
/// </remarks>
public sealed class LightExtract : IExtractSystem
{
    /// <inheritdoc />
    public void Run(World world, RenderWorld renderWorld)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;

        var lights = renderWorld.TryGet<RenderLights>() ?? new RenderLights();
        lights.All.Clear();
        renderWorld.Set(lights);

        foreach (var (entity, light) in ecs.Query<Light>())
        {
            // Identity when the entity has no transform, as an ambient light often has not.
            // Position and orientation in world space, composed through parents when it has one.
            Matrix4x4.Decompose(TransformPropagation.WorldMatrix(ecs, entity), out _, out var rotation, out var position);
            var t = new Transform(position, rotation, Vector3.One);

            // A directional or spot light points along its entity's -Z. Apply only the
            // rotation (translation goes into Position; scale is irrelevant for direction).
            var direction = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, t.Rotation));
            if (!float.IsFinite(direction.X)) direction = -Vector3.UnitZ;

            // An angle past 90 degrees has no cone to speak of, and an inner angle past the outer
            // one is taken as the outer, so the fade is never inverted.
            var outer = Math.Clamp(light.OuterAngle, 0f, 90f);
            var inner = Math.Clamp(light.InnerAngle, 0f, outer);
            var render = new RenderLight
            {
                MainEntityId = entity,
                Kind = light.Kind,
                Position = t.Position,
                Direction = direction,
                EmittedColor = light.Color * light.Intensity,
                Range = Math.Max(0f, light.Range),
                CosInner = MathF.Cos(float.DegreesToRadians(inner)),
                CosOuter = MathF.Cos(float.DegreesToRadians(outer)),
                CastsShadows = light.CastsShadows,
            };

            int renderEntity = renderWorld.Spawn();
            renderWorld.Entities.Add(renderEntity, render);
            lights.All.Add(render);
        }
    }
}