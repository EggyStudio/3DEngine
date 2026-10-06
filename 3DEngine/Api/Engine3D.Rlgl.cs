using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- rlgl, raylib's layer under its shapes: vertices given one at a time, and the matrix stack
    // that moves them. The functions raylib's examples call are carried under rlgl's names. The
    // vertices go into the frame's draw list, as every shape's do, so rlgl's batch, its buffers
    // and its OpenGL state have nothing to do.

    // The matrix rlgl moves each vertex by, its stack, and the corners given since the last primitive.
    private static Matrix4x4 _rlTransform = Matrix4x4.Identity;
    private static readonly Stack<Matrix4x4> RlStack = new();
    private static RlDrawMode? _rlMode;
    private static readonly ImmediateVertex[] RlCorners = new ImmediateVertex[4];
    private static int _rlCornerCount;
    private static Color _rlColor = Color.White;
    private static Vector2 _rlTexCoord;
    private static int _rlTexture;

    /// <summary>
    /// Keeps the current transform, which <see cref="rlPopMatrix"/> returns to, as rlgl's
    /// <c>rlPushMatrix</c> does. The shapes, text and models drawn until then are moved by the
    /// transforms set in between.
    /// </summary>
    public static void rlPushMatrix() => RlStack.Push(_rlTransform);

    /// <summary>Returns to the transform the last <see cref="rlPushMatrix"/> kept.</summary>
    public static void rlPopMatrix()
    {
        if (RlStack.TryPop(out var kept)) SetRlTransform(kept);
    }

    /// <summary>Moves what is drawn after by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before, as rlgl's <c>rlTranslatef</c> does.</summary>
    public static void rlTranslatef(float x, float y, float z) => SetRlTransform(Matrix4x4.CreateTranslation(x, y, z) * _rlTransform);

    /// <summary>Turns what is drawn after by <paramref name="angle"/> degrees about the axis (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before.</summary>
    public static void rlRotatef(float angle, float x, float y, float z)
    {
        var axis = new Vector3(x, y, z);
        if (axis.LengthSquared() == 0) return;
        SetRlTransform(Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), float.DegreesToRadians(angle)) * _rlTransform);
    }

    /// <summary>Scales what is drawn after by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before.</summary>
    public static void rlScalef(float x, float y, float z) => SetRlTransform(Matrix4x4.CreateScale(x, y, z) * _rlTransform);

    /// <summary>Starts giving vertices one at a time, joined as <paramref name="mode"/> says, until <see cref="rlEnd"/>.</summary>
    /// <remarks>
    /// Each vertex takes the color, texture coordinate and texture set before it, and is moved by
    /// the matrix stack, as rlgl's are. A primitive is recorded once its last vertex is given, so
    /// the vertices of one left unfinished at <see cref="rlEnd"/> draw nothing.
    /// </remarks>
    public static void rlBegin(RlDrawMode mode)
    {
        _rlMode = mode;
        _rlCornerCount = 0;
    }

    /// <summary>Ends the vertices <see cref="rlBegin"/> started.</summary>
    public static void rlEnd()
    {
        _rlMode = null;
        _rlCornerCount = 0;
    }

    /// <summary>Gives a vertex at (<paramref name="x"/>, <paramref name="y"/>), as rlgl's <c>rlVertex2f</c> does, for 2D.</summary>
    public static void rlVertex2f(float x, float y) => rlVertex3f(x, y, 0);

    /// <summary>Gives a vertex at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), as rlgl's <c>rlVertex3f</c> does.</summary>
    public static void rlVertex3f(float x, float y, float z)
    {
        if (_rlMode is not { } mode) return;
        RlCorners[_rlCornerCount++] = new ImmediateVertex(new Vector3(x, y, z), _rlTexCoord, _rlColor);
        var corners = mode switch { RlDrawMode.Lines => 2, RlDrawMode.Triangles => 3, _ => 4 };
        if (_rlCornerCount < corners) return;
        DrawList.Primitive(RlCorners.AsSpan(0, corners), _rlTexture);
        _rlCornerCount = 0;
    }

    /// <summary>Sets the texture coordinate of the vertices given after, from 0 to 1 across the texture.</summary>
    public static void rlTexCoord2f(float x, float y) => _rlTexCoord = new Vector2(x, y);

    /// <summary>
    /// Takes the normal of the vertices given after, as rlgl's <c>rlNormal3f</c> does, and keeps
    /// none, since the vertices of shapes drawn this way are not lit and carry no normal.
    /// </summary>
    public static void rlNormal3f(float x, float y, float z) { }

    /// <summary>Sets the color of the vertices given after.</summary>
    public static void rlColor4ub(byte r, byte g, byte b, byte a) => _rlColor = new Color(r, g, b, a);

    /// <summary>Sets the color of the vertices given after, from four values from 0 to 1.</summary>
    public static void rlColor4f(float r, float g, float b, float a) => _rlColor = ColorFromNormalized(new Vector4(r, g, b, a));

    /// <summary>
    /// Sets the texture of the primitives given after, by the id of a <see cref="Texture2D"/>, or
    /// 0 for none, which draws them in their colors alone, as rlgl's <c>rlSetTexture</c> does.
    /// </summary>
    public static void rlSetTexture(int id) => _rlTexture = id;

    /// <summary>
    /// Whether giving <paramref name="vertexCount"/> more vertices would draw what rlgl's batch held
    /// first, which is never, since the draw list grows to hold a frame. raylib's programs call it
    /// before a long run of vertices.
    /// </summary>
    public static bool rlCheckRenderBatchLimit(int vertexCount) => false;

    // Sets the transform rlgl moves vertices by, which the draw list moves every shape's by.
    private static void SetRlTransform(Matrix4x4 transform)
    {
        _rlTransform = transform;
        DrawList.SetModel(transform);
    }

    // A frame starts with no transform, nothing pushed and no vertices half given.
    internal static void ResetRlgl()
    {
        RlStack.Clear();
        SetRlTransform(Matrix4x4.Identity);
        (_rlMode, _rlCornerCount, _rlColor, _rlTexCoord, _rlTexture) = (null, 0, Color.White, Vector2.Zero, 0);
    }

    // A camera mode begun or ended with nothing pushed starts from no transform, as rlgl loads the
    // identity into the matrix a transform set without a push changes. One set after a push is
    // kept until its pop.
    private static void ResetRlglUnlessPushed()
    {
        if (RlStack.Count == 0 && !_rlTransform.IsIdentity) SetRlTransform(Matrix4x4.Identity);
    }
}
