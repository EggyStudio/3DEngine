namespace Engine;

/// <summary>Which faces <see cref="Engine3D.rlSetCullFace"/> leaves out, rlgl's <c>RL_CULL_FACE_FRONT</c> and <c>RL_CULL_FACE_BACK</c>.</summary>
public enum RlCullFace
{
    /// <summary>The front faces, counterclockwise on the screen, as an outline drawn from a model's inside leaves out.</summary>
    Front,
    /// <summary>The back faces, clockwise on the screen.</summary>
    Back,
}
