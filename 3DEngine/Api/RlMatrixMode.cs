namespace Engine;

/// <summary>Which of rlgl's matrices <see cref="Engine3D.rlMatrixMode"/> has the calls after it change, rlgl's <c>RL_MODELVIEW</c> and <c>RL_PROJECTION</c>.</summary>
public enum RlMatrixMode
{
    /// <summary>The view and the transforms that move what is drawn, which a camera mode sets the view of.</summary>
    Modelview = 0x1700,
    /// <summary>The projection from the view to the screen.</summary>
    Projection = 0x1701,
}
