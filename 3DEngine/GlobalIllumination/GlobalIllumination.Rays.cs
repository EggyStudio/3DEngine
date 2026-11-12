using static Engine.Illumination;

namespace Engine;

internal sealed partial class GlobalIlluminationRenderer
{
    // The window's meshes for the device's ray tracing, which a reflection the field misses is
    // traced through, at the top quality where the device and the model pass trace rays, a
    // skinned mesh, whose posed shape no vertices on the CPU hold, left out, and each marked
    // where global illumination carries it as a glow light, for a probe's reference.
    private void RecordRays(RenderContext renderContext, RenderWorld renderWorld, GraphicsDevice device, SceneFieldPlan? plan)
    {
        if (renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality == GlobalIllumination.High && device.CanQueryRays && renderWorld.TryGet<ModelRenderer>() is { TracesRays: true }
            && !renderWorld.TryGet<GlobalIlluminationSettings>()!.RaysOff
            && renderWorld.TryGet<SceneFieldRenderer>() is { } fields)
        {
            _rays ??= device.CreateRayScene();
            var copies = new List<RayInstance>(fields.Drawn.Count);
            // A mesh over shader buffers, whose triangles the CPU does not hold, is passed over.
            foreach (var (instance, skinned) in fields.Drawn)
                if (!skinned && fields.HoldsTriangles(instance.Vertices)) copies.Add(new RayInstance(instance.Vertices, instance.World, instance.Color, instance.Emission, instance.Roughness, instance.Metallic,
                    plan is not null && fields.Carried(instance, plan.CellSize), instance.LightThrough, instance.Texture, instance.NormalMap, instance.NormalScale, instance.Tint));
            try
            {
                _retired.Add((_frame, device.RecordRayScene(renderContext.CommandBuffer, _rays, copies, mesh => fields.CornersOf((ModelVertex[])mesh),
                    mesh => fields.CornerColorsOf((ModelVertex[])mesh), fields.Recolored, mesh => fields.UvsOf((ModelVertex[])mesh),
                    renderWorld.TryGet<RenderOrigin>())));
                GlobalIlluminationStages.Of(device).MarkGlobalIllumination(renderContext.CommandBuffer, "gi_rays");
            }
            catch (DeviceMemoryException e)
            {
                // The GPU's own rays are a part of the top quality the field stands in for, so where
                // their structures do not fit they are left off rather than the program stopping.
                Logger.Warn($"The GPU's own rays are left off, the scene's distance field tracing what they would. {e.Message}");
                renderWorld.TryGet<GlobalIlluminationSettings>()!.RaysOff = true;
                _retired.Add((_frame, _rays));
                _rays = null;
            }
        }
        else if (_rays is not null)
        {
            _retired.Add((_frame, _rays));
            _rays = null;
        }
    }
}
