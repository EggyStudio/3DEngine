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

    // Whether faces are culled: as the engine leaves them, until a program turns culling on or off,
    // then as it set. The engine's shapes draw both faces and a model the faces its material says,
    // where rlgl culls back faces from the start.
    private enum RlCulling { Unset, Enabled, Disabled }
    private static RlCulling _rlCulling;
    private static RlCullFace _rlCullFace = RlCullFace.Back;
    private static bool _rlPointMode;

    /// <summary>
    /// Leaves out the back faces of what is drawn after, or the front ones after
    /// <see cref="rlSetCullFace"/>, shapes and text as well as models, as rlgl's
    /// <c>rlEnableBackfaceCulling</c> does.
    /// </summary>
    /// <remarks>
    /// rlgl culls from the start, and a shape here draws both faces until this is called, so a
    /// triangle given clockwise shows here where raylib leaves it out. A model leaves out the
    /// faces its material says, the back ones unless it is double-sided, until a program calls
    /// this, <see cref="rlDisableBackfaceCulling"/> or <see cref="rlSetCullFace"/>, and the
    /// faces they say after, whatever its material says, as raylib's are.
    /// </remarks>
    public static void rlEnableBackfaceCulling()
    {
        _rlCulling = RlCulling.Enabled;
        ApplyRlCulling();
    }

    /// <summary>Draws both faces of what is drawn after, models whatever their material says, as rlgl's <c>rlDisableBackfaceCulling</c> does.</summary>
    public static void rlDisableBackfaceCulling()
    {
        _rlCulling = RlCulling.Disabled;
        ApplyRlCulling();
    }

    /// <summary>Sets which faces culling leaves out, the back ones unless set, as rlgl's <c>rlSetCullFace</c> does.</summary>
    public static void rlSetCullFace(RlCullFace mode)
    {
        _rlCullFace = mode;
        ApplyRlCulling();
    }

    /// <summary>
    /// Draws the models drawn after as a point at each corner of their triangles, as rlgl's
    /// <c>rlEnablePointMode</c> draws them, where the GPU's driver can, and filled where it cannot.
    /// </summary>
    /// <remarks>It applies to models alone, shapes being drawn filled whatever it says.</remarks>
    public static void rlEnablePointMode() => _rlPointMode = true;

    /// <summary>Draws the models drawn after filled again.</summary>
    public static void rlDisablePointMode() => _rlPointMode = false;

    // The faces the draw list leaves out of shapes: none until a program turns culling on.
    private static void ApplyRlCulling() =>
        DrawList.SetCull(_rlCulling == RlCulling.Enabled ? (_rlCullFace == RlCullFace.Front ? CullMode.Front : CullMode.Back) : CullMode.None);

    // Whether a program has set rlgl's culling, after which a model's faces are culled as rlgl
    // culls them, whatever its material says, as raylib's are.
    private static bool RlCullingSet => _rlCulling != RlCulling.Unset || _rlCullFace != RlCullFace.Back;

    // A model's draw with the faces rlgl's culling leaves out, once a program has set it, and
    // drawn as points in point mode.
    private static ModelDraw WithRlState(ModelDraw draw) =>
        !RlCullingSet && !_rlPointMode
            ? draw
            : draw with
            {
                DoubleSided = RlCullingSet ? _rlCulling == RlCulling.Disabled : draw.DoubleSided,
                CullFront = _rlCullFace == RlCullFace.Front,
                Points = _rlPointMode,
            };

    // The factors the custom blend modes combine by, as rlgl keeps them: one set for
    // BlendMode.Custom, the color's and the alpha's alike, and one for BlendMode.CustomSeparate.
    private static BlendFactors _rlBlendFactors = BlendFactors.Default;
    private static BlendFactors _rlBlendFactorsSeparate = BlendFactors.Default;

    /// <summary>
    /// Sets the factors and the equation <see cref="BlendMode.Custom"/> combines by, for the color
    /// and its alpha alike, as rlgl's <c>rlSetBlendFactors</c> does.
    /// </summary>
    /// <remarks>
    /// What is drawn in the custom mode after this takes them, whether the mode was set before or
    /// after, where rlgl's takes them from the next <see cref="rlSetBlendMode"/>.
    /// </remarks>
    public static void rlSetBlendFactors(RlBlendFactor glSrcFactor, RlBlendFactor glDstFactor, RlBlendEquation glEquation)
    {
        _rlBlendFactors = new BlendFactors(glSrcFactor, glDstFactor, glEquation, glSrcFactor, glDstFactor, glEquation);
        if (DrawList.Blend == BlendMode.Custom) DrawList.SetBlend(BlendMode.Custom, _rlBlendFactors);
    }

    /// <summary>
    /// Sets the factors and the equations <see cref="BlendMode.CustomSeparate"/> combines by, the
    /// color's apart from the alpha's, as rlgl's <c>rlSetBlendFactorsSeparate</c> does.
    /// </summary>
    /// <remarks>What is drawn in the custom mode after this takes them, as <see cref="rlSetBlendFactors"/> says.</remarks>
    public static void rlSetBlendFactorsSeparate(RlBlendFactor glSrcRGB, RlBlendFactor glDstRGB, RlBlendFactor glSrcAlpha, RlBlendFactor glDstAlpha,
        RlBlendEquation glEqRGB, RlBlendEquation glEqAlpha)
    {
        _rlBlendFactorsSeparate = new BlendFactors(glSrcRGB, glDstRGB, glEqRGB, glSrcAlpha, glDstAlpha, glEqAlpha);
        if (DrawList.Blend == BlendMode.CustomSeparate) DrawList.SetBlend(BlendMode.CustomSeparate, _rlBlendFactorsSeparate);
    }

    /// <summary>Lays what is drawn after over what is there by <paramref name="mode"/>, as <see cref="BeginBlendMode"/> does, under rlgl's name.</summary>
    public static void rlSetBlendMode(BlendMode mode) => BeginBlendMode(mode);

    /// <summary>
    /// Draws what rlgl's batch holds before what follows, as rlgl's <c>rlDrawRenderBatchActive</c>
    /// does, which here has nothing to do, since the draw list keeps the shapes of each state
    /// apart and in order.
    /// </summary>
    public static void rlDrawRenderBatchActive() { }

    // The factors a blend mode combines by, rlgl's for the custom modes and none for the rest.
    private static BlendFactors RlBlendFactorsFor(BlendMode mode) => mode switch
    {
        BlendMode.Custom => _rlBlendFactors,
        BlendMode.CustomSeparate => _rlBlendFactorsSeparate,
        _ => default,
    };

    // Sets the transform rlgl moves vertices by, which the draw list moves every shape's by.
    private static void SetRlTransform(Matrix4x4 transform)
    {
        _rlTransform = transform;
        DrawList.SetModel(transform);
    }

    // A frame starts with no transform, nothing pushed and no vertices half given. Culling and
    // point mode are kept, as rlgl's state is, and given to the draw list again.
    internal static void ResetRlgl()
    {
        RlStack.Clear();
        SetRlTransform(Matrix4x4.Identity);
        (_rlMode, _rlCornerCount, _rlColor, _rlTexCoord, _rlTexture) = (null, 0, Color.White, Vector2.Zero, 0);
        ApplyRlCulling();
    }

    // A window closed takes rlgl's state with it, so the next starts as the engine leaves faces.
    internal static void ForgetRlgl()
    {
        (_rlCulling, _rlCullFace, _rlPointMode) = (RlCulling.Unset, RlCullFace.Back, false);
        (_rlBlendFactors, _rlBlendFactorsSeparate) = (BlendFactors.Default, BlendFactors.Default);
        RlStack.Clear();
        _rlTransform = Matrix4x4.Identity;
    }

    // A camera mode begun or ended with nothing pushed starts from no transform, as rlgl loads the
    // identity into the matrix a transform set without a push changes. One set after a push is
    // kept until its pop.
    private static void ResetRlglUnlessPushed()
    {
        if (RlStack.Count == 0 && !_rlTransform.IsIdentity) SetRlTransform(Matrix4x4.Identity);
    }
}
