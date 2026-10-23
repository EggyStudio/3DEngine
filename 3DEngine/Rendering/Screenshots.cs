namespace Engine;

/// <summary>Captures the next presented frame, to a PNG file or to the code that reads it.</summary>
internal static class Screenshots
{
    /// <summary>
    /// Asks for the next frame the renderer presents to be written to <paramref name="path"/>, and
    /// calls <paramref name="done"/> on the render thread with <c>null</c> once it is written, or
    /// with the reason it was not.
    /// </summary>
    /// <returns>Why a capture cannot be taken at all, or <c>null</c> when it was asked for.</returns>
    public static string? Request(World world, string path, Action<string?>? done = null) =>
        Capture(world, (rgba, width, height) =>
        {
            try
            {
                PngWriter.Write(path, rgba, width, height);
                done?.Invoke(null);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                done?.Invoke($"The capture could not be written to {path}. {error.Message}");
            }
        });

    /// <summary>
    /// Asks for the next frame the renderer presents, its RGBA pixels from the top left handed to
    /// <paramref name="captured"/> on the render thread with its width and height, the capture a
    /// screenshot and <c>frame.read</c> take.
    /// </summary>
    /// <returns>Why a capture cannot be taken at all, or <c>null</c> when it was asked for.</returns>
    public static string? Capture(World world, Action<byte[], int, int> captured)
    {
        if (!world.TryGetResource<Renderer>(out var renderer) || !renderer.Context.IsInitialized)
            return "There is no renderer to capture from (a headless run draws nothing, and --offscreen draws with no window).";
        if (renderer.Context.Graphics is not GraphicsDevice device)
            return "The renderer's device cannot capture frames.";

        try
        {
            device.RequestCapture(captured);
            return null;
        }
        catch (NotSupportedException error)
        {
            return error.Message;
        }
    }
}
