using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Frame-based input state resource. Tracks keyboard, mouse, and text input.
/// <para>
/// Query methods (<c>KeyDown</c>, <c>MousePressed</c>, etc.) are public.
/// Mutation methods (<c>SetKey</c>, <c>SetMouseButton</c>, etc.) are <c>internal</c>
/// - only platform backends should call them.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Read input inside a behavior
/// [Behavior]
/// public partial struct PlayerController
/// {
///     [OnUpdate]
///     public static void HandleInput(BehaviorContext ctx)
///     {
///         if (ctx.Input.KeyPressed(Key.Space))
///             Console.WriteLine("Jump!");
///         if (ctx.Input.KeyDown(Key.W))
///             MoveForward();
///         if (ctx.Input.MousePressed(MouseButton.Left))
///             Shoot(ctx.Input.MouseX, ctx.Input.MouseY);
///
///         var (dx, dy) = ctx.Input.MouseDelta;
///         RotateCamera(dx, dy);
///     }
/// }
/// </code>
/// <code>
/// // Read input inside a raw system
/// app.AddSystem(Stage.Update, static world =>
/// {
///     var input = world.Resource&lt;Input&gt;();
///
///     if (input.KeyPressed(Key.Space))
///         Console.WriteLine("Jump!");
///     if (input.KeyDown(Key.W))
///         MoveForward();
///     if (input.MousePressed(MouseButton.Left))
///         Shoot(input.MouseX, input.MouseY);
///
///     var (dx, dy) = input.MouseDelta;
///     RotateCamera(dx, dy);
/// });
/// </code>
/// </example>
public sealed class Input
{
    private readonly HashSet<Key> _keysDown = [];
    private readonly HashSet<Key> _keysPressed = [];
    private readonly HashSet<Key> _keysReleased = [];

    private readonly HashSet<MouseButton> _mouseDown = [];
    private readonly HashSet<MouseButton> _mousePressed = [];
    private readonly HashSet<MouseButton> _mouseReleased = [];

    /// <summary>Absolute X position of the mouse cursor in window pixels.</summary>
    public int MouseX { get; private set; }

    /// <summary>Absolute Y position of the mouse cursor in window pixels.</summary>
    public int MouseY { get; private set; }

    /// <summary>Absolute mouse cursor position as an (X, Y) tuple in window pixels.</summary>
    public (int X, int Y) MousePosition => (MouseX, MouseY);

    /// <summary>Relative mouse X motion accumulated this frame.</summary>
    public int MouseDeltaX { get; private set; }

    /// <summary>Relative mouse Y motion accumulated this frame.</summary>
    public int MouseDeltaY { get; private set; }

    /// <summary>Relative mouse motion accumulated this frame as an (X, Y) tuple.</summary>
    public (int X, int Y) MouseDelta => (MouseDeltaX, MouseDeltaY);

    /// <summary>Horizontal scroll wheel input accumulated this frame.</summary>
    public float WheelX { get; private set; }

    /// <summary>Vertical scroll wheel input accumulated this frame.</summary>
    public float WheelY { get; private set; }

    private readonly List<char> _textInput = [];
    
    /// <summary>Characters typed this frame via text input events.</summary>
    public ReadOnlySpan<char> TextInput => CollectionsMarshal.AsSpan(_textInput);

    /// <summary>Returns <c>true</c> while the specified key is held down.</summary>
    /// <param name="key">The key to test.</param>
    /// <returns><c>true</c> if the key is currently held; otherwise <c>false</c>.</returns>
    public bool KeyDown(Key key) => _keysDown.Contains(key);

    /// <summary>Returns <c>true</c> during the frame the key was first pressed.</summary>
    /// <param name="key">The key to test.</param>
    /// <returns><c>true</c> if the key transitioned to down this frame; otherwise <c>false</c>.</returns>
    public bool KeyPressed(Key key) => _keysPressed.Contains(key);

    /// <summary>Returns <c>true</c> during the frame the key was released.</summary>
    /// <param name="key">The key to test.</param>
    /// <returns><c>true</c> if the key transitioned to up this frame; otherwise <c>false</c>.</returns>
    public bool KeyReleased(Key key) => _keysReleased.Contains(key);

    /// <summary>Returns <c>true</c> if any key is currently held down.</summary>
    /// <returns><c>true</c> if at least one key is held; otherwise <c>false</c>.</returns>
    public bool AnyKeyDown() => _keysDown.Count > 0;

    /// <summary>Returns <c>true</c> if any key was first pressed this frame.</summary>
    /// <returns><c>true</c> if at least one key transitioned to down; otherwise <c>false</c>.</returns>
    public bool AnyKeyPressed() => _keysPressed.Count > 0;

    /// <summary>Returns <c>true</c> while the specified mouse button is held down.</summary>
    /// <param name="button">The mouse button to test.</param>
    /// <returns><c>true</c> if the button is currently held; otherwise <c>false</c>.</returns>
    public bool MouseDown(MouseButton button) => _mouseDown.Contains(button);

    /// <summary>Returns <c>true</c> during the frame the mouse button was first pressed.</summary>
    /// <param name="button">The mouse button to test.</param>
    /// <returns><c>true</c> if the button transitioned to down this frame; otherwise <c>false</c>.</returns>
    public bool MousePressed(MouseButton button) => _mousePressed.Contains(button);

    /// <summary>Returns <c>true</c> during the frame the mouse button was released.</summary>
    /// <param name="button">The mouse button to test.</param>
    /// <returns><c>true</c> if the button transitioned to up this frame; otherwise <c>false</c>.</returns>
    public bool MouseReleased(MouseButton button) => _mouseReleased.Contains(button);

    /// <summary>Returns <c>true</c> if any mouse button is currently held down.</summary>
    /// <returns><c>true</c> if at least one button is held; otherwise <c>false</c>.</returns>
    public bool AnyMouseDown() => _mouseDown.Count > 0;

    /// <summary>Returns <c>true</c> if any mouse button was first pressed this frame.</summary>
    /// <returns><c>true</c> if at least one button transitioned to down; otherwise <c>false</c>.</returns>
    public bool AnyMousePressed() => _mousePressed.Count > 0;

    /// <summary>Returns <c>true</c> while the specified mouse button (by 0-based index) is held down.</summary>
    /// <param name="button">Zero-based button index (0 = left, 1 = middle, 2 = right).</param>
    /// <returns><c>true</c> if the button is currently held; otherwise <c>false</c>.</returns>
    public bool MouseDown(int button) => _mouseDown.Contains((MouseButton)button);

    /// <summary>Returns <c>true</c> during the frame the mouse button (by 0-based index) was first pressed.</summary>
    /// <param name="button">Zero-based button index.</param>
    /// <returns><c>true</c> if the button transitioned to down this frame; otherwise <c>false</c>.</returns>
    public bool MousePressed(int button) => _mousePressed.Contains((MouseButton)button);

    /// <summary>Returns <c>true</c> during the frame the mouse button (by 0-based index) was released.</summary>
    /// <param name="button">Zero-based button index.</param>
    /// <returns><c>true</c> if the button transitioned to up this frame; otherwise <c>false</c>.</returns>
    public bool MouseReleased(int button) => _mouseReleased.Contains((MouseButton)button);

    // -- Mutation (internal - platform backends only) --

    /// <summary>Clears per-frame transient state. Called once at the end of each frame.</summary>
    internal void BeginFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mousePressed.Clear();
        _mouseReleased.Clear();
        MouseDeltaX = 0;
        MouseDeltaY = 0;
        WheelX = 0;
        WheelY = 0;
        _textInput.Clear();
    }

    /// <summary>Updates the state of a keyboard key.</summary>
    /// <param name="key">The key whose state changed.</param>
    /// <param name="isDown"><c>true</c> if the key is now held; <c>false</c> if released.</param>
    internal void SetKey(Key key, bool isDown)
    {
        if (isDown)
        {
            if (_keysDown.Add(key)) _keysPressed.Add(key);
        }
        else
        {
            if (_keysDown.Remove(key)) _keysReleased.Add(key);
        }
    }

    /// <summary>Updates the state of a mouse button.</summary>
    /// <param name="button">The mouse button whose state changed.</param>
    /// <param name="isDown"><c>true</c> if the button is now held; <c>false</c> if released.</param>
    internal void SetMouseButton(MouseButton button, bool isDown)
    {
        if (isDown)
        {
            if (_mouseDown.Add(button)) _mousePressed.Add(button);
        }
        else
        {
            if (_mouseDown.Remove(button)) _mouseReleased.Add(button);
        }
    }

    /// <summary>Updates the state of a mouse button by 0-based index.</summary>
    /// <param name="button">Zero-based button index.</param>
    /// <param name="isDown"><c>true</c> if the button is now held; <c>false</c> if released.</param>
    internal void SetMouseButton(int button, bool isDown) => 
        SetMouseButton((MouseButton)button, isDown);

    /// <summary>Sets the absolute mouse position in window pixels.</summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    internal void SetMousePosition(int x, int y)
    {
        MouseX = x;
        MouseY = y;
    }

    /// <summary>Accumulates relative mouse motion for this frame.</summary>
    /// <param name="dx">Horizontal pixel delta.</param>
    /// <param name="dy">Vertical pixel delta.</param>
    internal void AddMouseDelta(int dx, int dy)
    {
        MouseDeltaX += dx;
        MouseDeltaY += dy;
    }

    /// <summary>Accumulates scroll wheel input for this frame.</summary>
    /// <param name="dx">Horizontal scroll amount.</param>
    /// <param name="dy">Vertical scroll amount.</param>
    internal void AddWheel(float dx, float dy)
    {
        WheelX += dx;
        WheelY += dy;
    }

    /// <summary>Appends typed characters for this frame.</summary>
    /// <param name="text">The text input string from the platform.</param>
    internal void AddText(string text)
    {
        foreach (var c in text)
            _textInput.Add(c);
    }

    // -- Diagnostics --

    /// <summary>Human-readable snapshot for debugging.</summary>
    public override string ToString() => 
        $"Input {{ Keys={_keysDown.Count} down, Mouse=({MouseX},{MouseY}), Buttons={_mouseDown.Count} down }}";
}
