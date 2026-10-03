using SDL3;

namespace Engine;

/// <summary>Frame-based input state (keys, mouse, deltas) derived from SDL events.</summary>
/// <remarks>
/// Maintains three key sets per frame: <em>down</em> (held this frame), <em>pressed</em>
/// (transitioned to down this frame), and <em>released</em> (transitioned to up this frame).
/// Call <see cref="BeginFrame"/> at the start of each frame to clear transient state, then
/// <see cref="Process"/> for each SDL event, and optionally <see cref="EndFrame"/> at the end.
/// </remarks>
/// <seealso cref="AppWindow"/>
/// <seealso cref="IInputBackend"/>
public sealed class SdlInput
{
    private readonly HashSet<SDL.Scancode> _down = new();
    private readonly HashSet<SDL.Scancode> _pressed = new();
    private readonly HashSet<SDL.Scancode> _released = new();

    /// <summary>Current mouse X position in window coordinates.</summary>
    public int MouseX { get; private set; }
    /// <summary>Current mouse Y position in window coordinates.</summary>
    public int MouseY { get; private set; }
    /// <summary>Mouse X delta since the last frame.</summary>
    public int MouseDeltaX { get; private set; }
    /// <summary>Mouse Y delta since the last frame.</summary>
    public int MouseDeltaY { get; private set; }
    /// <summary>Currently held mouse button flags.</summary>
    public SDL.MouseButtonFlags MouseButtons { get; private set; }

    /// <summary>Returns <c>true</c> if the specified key is currently held down.</summary>
    /// <param name="code">The key to test.</param>
    /// <returns><c>true</c> if the key is in the <em>down</em> set.</returns>
    public bool KeyDown(Key code) => _down.Contains((SDL.Scancode)code);
    /// <summary>Returns <c>true</c> if the specified key was pressed this frame (transition from up to down).</summary>
    /// <param name="code">The key to test.</param>
    /// <returns><c>true</c> if the key is in the <em>pressed</em> set.</returns>
    public bool KeyPressed(Key code) => _pressed.Contains((SDL.Scancode)code);
    /// <summary>Returns <c>true</c> if the specified key was released this frame (transition from down to up).</summary>
    /// <param name="code">The key to test.</param>
    /// <returns><c>true</c> if the key is in the <em>released</em> set.</returns>
    public bool KeyReleased(Key code) => _released.Contains((SDL.Scancode)code);

    /// <summary>Clears per-frame transient state (pressed/released sets and mouse deltas). Call at frame start.</summary>
    public void BeginFrame()
    {
        _pressed.Clear();
        _released.Clear();
        MouseDeltaX = 0; MouseDeltaY = 0;
    }

    /// <summary>Finalizes the frame's input state. Currently a no-op; reserved for future use.</summary>
    public void EndFrame() { }

    /// <summary>Processes a single SDL event and updates internal key/mouse state.</summary>
    /// <param name="evt">The SDL event to process.</param>
    public void Process(SDL.Event evt)
    {
        switch ((SDL.EventType)evt.Type)
        {
            case SDL.EventType.KeyDown:
                var keyDown = evt.Key;
                if (!_down.Contains(keyDown.Scancode)) _pressed.Add(keyDown.Scancode);
                _down.Add(keyDown.Scancode);
                break;
            case SDL.EventType.KeyUp:
                var keyUp = evt.Key;
                _down.Remove(keyUp.Scancode);
                _released.Add(keyUp.Scancode);
                break;
            case SDL.EventType.MouseMotion:
                MouseDeltaX += (int)evt.Motion.XRel;
                MouseDeltaY += (int)evt.Motion.YRel;
                MouseX = (int)evt.Motion.X;
                MouseY = (int)evt.Motion.Y;
                break;
            case SDL.EventType.MouseButtonDown:
            case SDL.EventType.MouseButtonUp:
                MouseButtons = SDL.GetMouseState(out float x, out float y);
                MouseX = (int)x; MouseY = (int)y;
                break;
        }
    }
}