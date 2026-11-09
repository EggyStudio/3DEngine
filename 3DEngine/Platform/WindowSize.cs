namespace Engine;

/// <summary>
/// The window's size, inserted beside the window under <see cref="IWindowSize"/> rather than the
/// window inserted twice, since the world disposes each resource it holds once a key.
/// </summary>
internal sealed class WindowSize(AppWindow window) : IWindowSize
{
    public int Width => window.Sdl.Width;

    public int Height => window.Sdl.Height;
}
