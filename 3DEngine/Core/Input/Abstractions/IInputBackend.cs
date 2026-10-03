namespace Engine;

/// <summary>
/// Backend contract for platform-specific input event forwarding.
/// Implementations bridge platform input (SDL, WinForms, etc.) into the engine's
/// <see cref="Input"/> resource during <see cref="Initialize"/>.
/// </summary>
/// <remarks>
/// A single <see cref="IInputBackend"/> is registered as a <see cref="World"/> resource by
/// the platform layer (e.g., the SDL application plugin). The <see cref="InputPlugin"/> looks
/// up this resource during its <see cref="IPlugin.Build"/> phase and calls
/// <see cref="Initialize"/> to wire platform events into the engine's <see cref="Input"/> state.
/// </remarks>
/// <seealso cref="Input"/>
/// <seealso cref="InputPlugin"/>
public interface IInputBackend
{
    /// <summary>Wires platform-specific input events to the engine's <see cref="Input"/> resource.</summary>
    /// <param name="app">The application instance, for accessing the world and other resources.</param>
    /// <param name="input">The engine's input state resource to forward events into.</param>
    void Initialize(App app, Input input);
}
