using System.Runtime.InteropServices;
using SDL3;

namespace Engine;

/// <summary>
/// Creates the application window from <see cref="Config"/>, inserts it as a resource,
/// and provides the <see cref="IMainLoopDriver"/> and <see cref="IInputBackend"/>.
/// </summary>
/// <remarks>
/// When the graphics backend is <see cref="GraphicsBackend.Vulkan"/>, an
/// <see cref="ISurfaceSource"/> is also registered so the renderer can create a Vulkan surface.
/// </remarks>
/// <seealso cref="AppWindow"/>
/// <seealso cref="Config"/>
/// <seealso cref="IMainLoopDriver"/>
/// <seealso cref="IInputBackend"/>
public sealed class AppWindowPlugin : IPlugin
{
    /// <inheritdoc />
    /// <remarks>
    /// Foundational: provides <see cref="AppWindow"/>, <see cref="IMainLoopDriver"/>,
    /// <see cref="IInputBackend"/>, and (for Vulkan) <see cref="ISurfaceSource"/>. Plugins
    /// like <c>SdlPlugin</c>, <c>SdlImGuiPlugin</c> and <c>AppExitPlugin</c> read these
    /// resources, so this plugin sits in the foundation band and consumers don't need to
    /// declare it via <see cref="IPlugin.Dependencies"/>.
    /// </remarks>
    public int Order => PluginOrder.Foundation + 100;
    
    /// <summary>SDL-backed main loop driver that pumps events via <see cref="AppWindow.Looping"/>.</summary>
    private sealed class SdlMainLoopDriver(AppWindow window) : IMainLoopDriver
    {
        /// <inheritdoc />
        public void Run(Action frameStep)
        {
            window.Looping(frameStep);
        }

        public bool PumpEvents() => window.PollEvents();

        /// <inheritdoc />
        public void Shutdown()
        {
            // Called by App.Run() after the Cleanup stage, so all GPU
            // resources have been released before the platform window goes away.
            window.Dispose(null);
        }
    }

    /// <summary>SDL input backend forwarding keyboard, mouse, and text events into the engine <see cref="Input"/> resource.</summary>
    private sealed class SdlInputBackend : IInputBackend
    {
        /// <inheritdoc />
        public void Initialize(App app, Input input)
        {
            // Hook SDL events to update input; AppWindow already pumps events but we add handlers.
            var win = app.World.Resource<AppWindow>();
            win.SDLEvent += e => ProcessInputEvent(e, input);
            win.EventsPolled += input.ApplyQueued;
        }

        private static void ProcessInputEvent(SDL.Event e, Input input)
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.MouseMotion:
                    input.SetMousePosition((int)e.Motion.X, (int)e.Motion.Y);
                    input.AddMouseDelta((int)e.Motion.XRel, (int)e.Motion.YRel);
                    break;
                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    int btn = e.Button.Button - 1; // SDL 1..5 -> 0..4
                    if (btn is >= 0 and < 5)
                        input.SetMouseButton((MouseButton)btn, (SDL.EventType)e.Type == SDL.EventType.MouseButtonDown);
                    break;
                case SDL.EventType.MouseWheel:
                    input.AddWheel(e.Wheel.X, e.Wheel.Y);
                    break;
                case SDL.EventType.TextInput:
                    if (e.Text.Text != IntPtr.Zero)
                    {
                        var s = Marshal.PtrToStringUTF8(e.Text.Text);
                        if (!string.IsNullOrEmpty(s)) input.AddText(s);
                    }
                    break;
                case SDL.EventType.GamepadAdded:
                    {
                        var handle = SDL.OpenGamepad(e.GDevice.Which);
                        var name = handle == 0 ? "Gamepad" : SDL.GetGamepadName(handle) ?? "Gamepad";
                        input.ConnectGamepad(e.GDevice.Which, name, handle);
                    }
                    break;
                case SDL.EventType.GamepadRemoved:
                    {
                        var handle = input.DisconnectGamepad(e.GDevice.Which);
                        if (handle != 0) SDL.CloseGamepad(handle);
                    }
                    break;
                case SDL.EventType.GamepadButtonDown:
                case SDL.EventType.GamepadButtonUp:
                    input.GamepadById(e.GButton.Which)?.SetButton((GamepadButton)e.GButton.Button,
                        (SDL.EventType)e.Type == SDL.EventType.GamepadButtonDown);
                    break;
                case SDL.EventType.GamepadAxisMotion:
                    {
                        // Sticks report -32768 to 32767 and triggers 0 to 32767, so both scale by
                        // 32767 into -1 to 1 and 0 to 1.
                        var value = Math.Clamp(e.GAxis.Value / 32767f, -1f, 1f);
                        input.GamepadById(e.GAxis.Which)?.SetAxis((GamepadAxis)e.GAxis.Axis, value);
                    }
                    break;
                case SDL.EventType.KeyDown:
                case SDL.EventType.KeyUp:
                    bool down = (SDL.EventType)e.Type == SDL.EventType.KeyDown;
                    var mapped = (Key)e.Key.Scancode;
                    if (mapped != Key.Unknown)
                        input.SetKey(mapped, down);
                    break;
            }
        }
    }

    /// <inheritdoc />
    public void Build(App app)
    {
        var logger = Log.Category("Engine.Application");
        var config = app.World.Resource<Config>();

        if (config.Headless)
        {
            logger.Info($"AppWindowPlugin: Headless run - no window, frames paced at {config.HeadlessFps} per second.");
            app.World.InitResource<AppExit>();
            app.World.InsertResource<IMainLoopDriver>(new HeadlessLoopDriver(app.World, config.HeadlessFps));
            return;
        }

        logger.Info($"AppWindowPlugin: Creating window \"{config.WindowData.Title}\" ({config.WindowData.Width}x{config.WindowData.Height}) with backend={config.Graphics}...");
        var window = new AppWindow(config.WindowData, config.Graphics);
        SDL.SetWindowResizable(window.Sdl.Window, config.Resizable);
        SDL.SetWindowBordered(window.Sdl.Window, !config.Undecorated);
        SDL.SetWindowAlwaysOnTop(window.Sdl.Window, config.Topmost);

        if (config.Hidden)
        {
            logger.Info("Hidden run - the window is created and drawn into, and never shown.");
        }
        else
        {
            logger.Info($"Showing window with command: {config.WindowCommand}");
            window.Show(config.WindowCommand);
            if (config.Fullscreen) SDL.SetWindowFullscreen(window.Sdl.Window, true);
        }

        // SDL3 sends no text events until text input is started, unlike SDL2, so typed characters
        // reach neither ImGui's fields nor GetCharPressed without this. On a desktop it changes
        // nothing else. On a touch device it would raise the on-screen keyboard.
        if (!SDL.StartTextInput(window.Sdl.Window))
            logger.Warn($"AppWindowPlugin: StartTextInput failed, so typed text will not arrive: {SDL.GetError()}");

        app.World.InsertResource(window);
        app.World.InsertResource<IMainLoopDriver>(new SdlMainLoopDriver(window));
        app.World.InsertResource<IInputBackend>(new SdlInputBackend());
        logger.Info("AppWindow, IMainLoopDriver, and IInputBackend resources registered.");

        // When Vulkan is selected, provide the surface source so the renderer plugin can initialize.
        if (config.Graphics == GraphicsBackend.Vulkan)
        {
            app.World.InsertResource<ISurfaceSource>(new SdlSurfaceSource(window.Sdl));
            logger.Info("Vulkan surface source (SdlSurfaceSource) registered for renderer.");
        }
    }
}