using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>
/// Places the input method's composition window at the ImGui text field being typed into, so
/// characters a language composes from several keys are composed where they will land.
/// </summary>
/// <remarks>
/// Dear ImGui reports where its text cursor is through <c>Platform_SetImeDataFn</c> when that
/// changes, and the window's text input area is set from it, in the window's coordinates, which
/// ImGui's display size matches. With no field focused the area is cleared.
/// </remarks>
internal static unsafe class SdlImGuiIme
{
    private static nint _window;

    /// <summary>The last area set, or null after it was cleared, for tests.</summary>
    internal static (Vector2 Position, float LineHeight)? LastArea { get; private set; }

    // ImGuiPlatformImeData as Dear ImGui 1.91 lays it out.
    [StructLayout(LayoutKind.Sequential)]
    internal struct ImeData
    {
        public byte WantVisible;
        public Vector2 InputPos;
        public float InputLineHeight;
    }

    /// <summary>Hands ImGui the callback, for <paramref name="window"/>, or none with no window.</summary>
    public static void Install(nint window)
    {
        _window = window;
        LastArea = null;
        ImGui.GetPlatformIO().Platform_SetImeDataFn = (nint)(delegate* unmanaged[Cdecl]<nint, nint, ImeData*, void>)&SetImeData;
    }

    // Called from ImGui's native code, where an exception would end the process. A text box whose
    // input area could not be set types as before, only without the candidate list beside it.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetImeData(nint context, nint viewport, ImeData* data)
    {
        try
        {
            Apply(*data);
        }
        catch (Exception)
        {
            // ImGui asks for no answer.
        }
    }

    /// <summary>Sets or clears the window's text input area from what ImGui reports.</summary>
    internal static void Apply(in ImeData data)
    {
        if (data.WantVisible != 0)
        {
            LastArea = (data.InputPos, data.InputLineHeight);
            if (_window == 0) return;
            var rect = new SDL.Rect { X = (int)data.InputPos.X, Y = (int)data.InputPos.Y, W = 1, H = (int)MathF.Ceiling(data.InputLineHeight) };
            SDL.SetTextInputArea(_window, in rect, 0);
        }
        else
        {
            LastArea = null;
            if (_window != 0) SDL.SetTextInputArea(_window, 0, 0);
        }
    }
}
