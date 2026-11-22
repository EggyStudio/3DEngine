using Diagnostics;

namespace Engine;

/// <summary>
/// A command that saves what each of the window's ambient occlusion's passes holds as a picture,
/// so the horizons and the circular occluders, which the <c>ao.</c> settings choose between, can
/// be set beside a traced reference.
/// </summary>
internal static class AmbientOcclusionCommands
{
    [Command("ao.save", "Saves a picture of the window's ambient occlusion as the last frame left it, after its history and blurs (final), or the circular occluders' as they traced it (raw), as their filter left it (filtered) or their bent normal in view space (bent), a pixel of each texel of the depth at half the window's size: ao.save <png> <final|raw|filtered|bent>")]
    internal static string Save(string path, string what)
    {
        var world = ConsoleHost.World!;
        var renderer = world.Resource<Renderer>();
        if (renderer.Context.Graphics is not GraphicsDevice device || renderer.RenderWorld.TryGet<AmbientOcclusionRenderer>() is not { } occlusion)
            return "no device draws the window";
        var circular = occlusion.CircularPart;
        var (image, channel) = what switch
        {
            "final" => (occlusion.WindowOcclusion, 0),
            "raw" => (circular?.Raw, 3),
            "filtered" => (circular?.Filtered, 3),
            "bent" => (circular?.Raw, -1),
            _ => (null, 0),
        };
        if (what is not ("final" or "raw" or "filtered" or "bent")) return $"nothing named {what}, which is final, raw, filtered or bent";
        if (what == "filtered" && world.TryGetResource<AmbientOcclusionSettings>(out var settings) && !settings.OwnFilter)
            return "the circular occluders' own filter is off, so no pass writes what it holds, which ao.filter on turns on";
        if (image is null)
            return what == "final" ? "the window's ambient occlusion is not worked out, which SetAmbientOcclusion turns on" : "the circular occluders have drawn nothing, which ao.method circular chooses";
        var pixels = device.ReadPixels(image);
        if (channel >= 0)
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (pixels[i + channel], pixels[i + channel], pixels[i + channel], 255);
        else
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var size = image.Description.Extent;
        PngWriter.Write(path, pixels, (int)size.Width, (int)size.Height);
        return $"saved {what} at {size.Width} by {size.Height} as {path}";
    }
}
