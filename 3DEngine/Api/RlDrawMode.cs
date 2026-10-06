namespace Engine;

/// <summary>How <see cref="Engine3D.rlBegin"/> joins the vertices that follow, rlgl's <c>RL_LINES</c>, <c>RL_TRIANGLES</c> and <c>RL_QUADS</c>.</summary>
public enum RlDrawMode
{
    /// <summary>A line from each two vertices.</summary>
    Lines,
    /// <summary>A triangle from each three vertices.</summary>
    Triangles,
    /// <summary>A quad from each four vertices, in order around its edge, drawn as two triangles.</summary>
    Quads,
}
