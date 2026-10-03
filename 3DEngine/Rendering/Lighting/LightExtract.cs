using System.Numerics;

namespace Engine;

/// <summary>
/// Extracts entities with a <see cref="Light"/> component (and optionally <see cref="Transform"/>
/// + <see cref="LightShadow"/> + <see cref="LightShaping"/>) into render entities carrying
/// <see cref="RenderLight"/>, plus a flat <see cref="RenderLights"/> singleton on the
/// <see cref="RenderWorld"/>. Mirrors the <c>MeshMaterialExtract</c> pattern.
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
            // Identity transform when the entity has none (e.g. dome lights authored at
            // the stage root with no Xform parent).
            Transform t = default;
            ecs.TryGet(entity, out t);

            // UsdLux convention: distant / spot lights emit along -Z. Apply only the
            // rotation (translation goes into Position; scale is irrelevant for direction).
            var direction = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, t.Rotation));
            if (!float.IsFinite(direction.X)) direction = -Vector3.UnitZ;

            ecs.TryGet<LightShadow>(entity, out var shadow);
            ecs.TryGet<LightShaping>(entity, out var shaping);

            var render = new RenderLight
            {
                MainEntityId = entity,
                Type = light.Type,
                Position = t.Position,
                Direction = direction,
                EmittedColor = LightColor.ComputeEmittedColor(light),
                Radius = light.Radius ?? 0f,
                Width = light.Width ?? 0f,
                Height = light.Height ?? 0f,
                Length = light.Length ?? 0f,
                DomeTexture = light.DomeTexture,
                RectTexture = light.RectTexture,

                // Defaults: shadows on, no spot. Component-presence flips the override.
                CastsShadows = shadow.Enable ?? true,
                ShadowColor = shadow.Color ?? Vector3.Zero,
                HasCone = shaping.ConeAngle.HasValue,
                ConeAngle = shaping.ConeAngle ?? 0f,
                ConeSoftness = shaping.ConeSoftness ?? 0f,
            };

            int renderEntity = renderWorld.Spawn();
            renderWorld.Entities.Add(renderEntity, render);
            lights.All.Add(render);
        }
    }
}