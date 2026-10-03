namespace Engine;

/// <summary>Captures the next presented frame to a PNG file.</summary>
public static class Screenshots
{
    /// <summary>
    /// Asks for the next frame the renderer presents to be written to <paramref name="path"/>, and
    /// calls <paramref name="done"/> on the render thread with <c>null</c> once it is written, or
    /// with the reason it was not.
    /// </summary>
    /// <returns>Why a capture cannot be taken at all, or <c>null</c> when it was asked for.</returns>
    public static string? Request(World world, string path, Action<string?>? done = null)
    {
        if (!world.TryGetResource<Renderer>(out var renderer) || !renderer.Context.IsInitialized)
            return "There is no renderer to capture from (a headless run draws nothing, and --offscreen draws with no window).";
        if (renderer.Context.Graphics is not GraphicsDevice device)
            return "The renderer's device cannot capture frames.";

        try
        {
            device.RequestCapture((rgba, width, height) =>
            {
                try
                {
                    PngWriter.Write(path, rgba, width, height);
                    done?.Invoke(null);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    done?.Invoke($"The capture could not be written to {path}: {error.Message}");
                }
            });
            return null;
        }
        catch (NotSupportedException error)
        {
            return error.Message;
        }
    }
}
