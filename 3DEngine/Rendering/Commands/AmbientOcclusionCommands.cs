namespace Engine;

/// <summary>
/// Commands that choose how the window's ambient occlusion is worked out and save what each of
/// its passes holds as a picture, so the horizons and the circular occluders can be set beside a
/// traced reference.
/// </summary>
internal static class AmbientOcclusionCommands
{
    [Command("ao.method", "Works the window's ambient occlusion out by the horizons or by the circular occluders at 8 or 16 points a pixel: ao.method <horizons|circular> [samples]")]
    internal static string Method(string method, int samples = 16)
    {
        var chosen = method switch
        {
            "horizons" => AmbientOcclusionMethod.Horizons,
            "circular" => AmbientOcclusionMethod.CircularOccluders,
            _ => (AmbientOcclusionMethod?)null,
        };
        if (chosen is null) return $"no method {method}, which is horizons or circular";
        var settings = ConsoleHost.World!.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        (settings.Method, settings.Samples) = (chosen.Value, samples == 8 ? 8 : 16);
        return settings.Method == AmbientOcclusionMethod.Horizons ? "by the horizons" : $"by the circular occluders at {settings.Samples} points a pixel";
    }

    [Command("ao.filter", "Whether the circular occluders' own filter pass blends each pixel with its neighbors before the history takes it: ao.filter <on|off>")]
    internal static string Filter(string state)
    {
        if (state is not ("on" or "off")) return "on or off";
        var settings = ConsoleHost.World!.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        settings.OwnFilter = state == "on";
        return settings.OwnFilter ? "the circular occluders' own filter on" : "the circular occluders' own filter off";
    }

    [Command("ao.set", "Sets how the circular occluders take their circles, the most a slanted circle is grown (slope, 1 or more), the cosine past which a circle is a thin surface's back (backface, 0 to 1), how much such a back is grown (boost, 0 to 1), or the share of the picture's height a circle may span (screen), each held to its range: ao.set <slope|backface|boost|screen> <value>")]
    internal static string Set(string name, float value)
    {
        var settings = ConsoleHost.World!.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        if (float.IsNaN(value)) return "a number";
        switch (name)
        {
            case "slope": settings.SlopeCompensation = Math.Max(1, value); return $"slanted circles grown {settings.SlopeCompensation} times at most";
            case "backface": settings.BackfaceThreshold = Math.Clamp(value, 0, 1); return $"a circle leaning toward the pixel by a cosine of {settings.BackfaceThreshold} or more a back";
            case "boost": settings.BackfaceBoost = Math.Clamp(value, 0, 1); return $"a back grown by {settings.BackfaceBoost} of the compensation";
            case "screen": settings.ScreenShare = Math.Clamp(value, 0.01f, 1); return $"a circle spanning {settings.ScreenShare} of the picture's height at most";
            default: return $"nothing named {name}, which is slope, backface, boost or screen";
        }
    }

    [Command("ao.size", "Draws the window's depth the occlusion and the screen's probes read at half the window's size or at its own, so what each costs can be read: ao.size <half|full>")]
    internal static string Size(string size)
    {
        if (size is not ("half" or "full")) return "half or full";
        var settings = ConsoleHost.World!.GetOrInsertResource(static () => new AmbientOcclusionSettings());
        settings.FullSize = size == "full";
        return settings.FullSize ? "the window's depth at the window's size" : "the window's depth at half the window's size";
    }

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
