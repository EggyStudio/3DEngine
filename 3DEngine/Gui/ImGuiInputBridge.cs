using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>
/// ImGui's side of input for the code below the GUI, whether it holds the pointer, and the input the
/// program makes, as the console's, handed to ImGui while it has a context.
/// </summary>
internal sealed class ImGuiInputBridge : IGuiInput
{
    private static bool HasContext => ImGui.GetCurrentContext() != IntPtr.Zero;

    /// <inheritdoc />
    public bool HasPointer => HasContext && ImGui.GetIO().WantCaptureMouse;

    /// <inheritdoc />
    public void Key(Key key, bool down)
    {
        var imGuiKey = SdlImGuiInput.SdlKeyToImGuiKey((SDL.Scancode)(int)key);
        if (imGuiKey != ImGuiKey.None && HasContext) ImGui.GetIO().AddKeyEvent(imGuiKey, down);
    }

    /// <inheritdoc />
    public void Pointer(float x, float y)
    {
        if (HasContext) SdlImGuiInput.AddMousePos(x, y);
    }

    /// <inheritdoc />
    public void Button(MouseButton button, bool down)
    {
        if (HasContext) ImGui.GetIO().AddMouseButtonEvent(SdlImGuiInput.ImGuiButton(button), down);
    }

    /// <inheritdoc />
    public void Text(string text)
    {
        if (HasContext) ImGui.GetIO().AddInputCharactersUTF8(text);
    }

    /// <inheritdoc />
    public void Wheel(float amount)
    {
        if (HasContext) ImGui.GetIO().AddMouseWheelEvent(0, amount);
    }
}
