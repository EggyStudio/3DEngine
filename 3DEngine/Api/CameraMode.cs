using System.Numerics;

namespace Engine;

/// <summary>How <see cref="Engine3D.UpdateCamera"/> moves a camera from input.</summary>
public enum CameraMode
{
    /// <summary>
    /// The mouse and the arrow keys turn, Q and E roll, W, A, S and D move the way it looks, Space
    /// rises and left Ctrl sinks, dragging with the middle button pans, and the wheel and the
    /// keypad's plus and minus move it nearer its target and farther.
    /// </summary>
    Free,

    /// <summary>The camera circles its target half a radian a second, and the wheel moves it nearer and farther.</summary>
    Orbital,

    /// <summary>
    /// The mouse and the arrow keys turn the camera, Q and E roll it, and W, A, S and D walk along
    /// the ground, the plane across <see cref="Camera3D.Up"/>. Meant with <see cref="Engine3D.DisableCursor"/>.
    /// </summary>
    FirstPerson,

    /// <summary>
    /// The camera turns about its target with the mouse and the arrow keys, W, A, S and D walk the
    /// target along the ground, and the wheel moves the camera nearer and farther.
    /// </summary>
    ThirdPerson,

    /// <summary>Nothing moves the camera, which the program moves by its own means, as raylib's custom mode.</summary>
    Custom,
}
