namespace Engine;

/// <summary>Commands that say what the model pass did with the frame's instances.</summary>
internal static class ModelCommands
{
    [Command("models.blocks", "The instances of mesh entities the last frame copied into the ring, and how many of them were in blocks of 64 a pass drew, the camera's, a shadow's or a light's")]
    internal static string Blocks()
    {
        if (!ConsoleHost.World!.TryGetResource<Renderer>(out var renderer) || renderer.RenderWorld.TryGet<ModelRenderer>() is not { } models)
            return "no models are drawn";
        var (copied, drawn) = models.BlocksLastFrame();
        return copied == 0 ? "no instances were copied"
            : $"{copied} copied, {drawn} in blocks a pass drew, {copied - drawn} ({100.0 * (copied - drawn) / copied:0.0}%) in blocks none drew";
    }
}
