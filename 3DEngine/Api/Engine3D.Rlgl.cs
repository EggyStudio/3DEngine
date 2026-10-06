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

    // rlgl's projection and the view a camera mode set, which the draw list's transform is made
    // of, the view first, and which of rlgl's matrices the calls after rlMatrixMode change. The
    // model transform the stack builds moves each vertex as it is recorded.
    private static Matrix4x4 _rlView = Matrix4x4.Identity;
    private static Matrix4x4 _rlProjection = Matrix4x4.Identity;
    private static RlMatrixMode _rlMatrixMode = RlMatrixMode.Modelview;
    private static readonly Stack<Matrix4x4> RlProjectionStack = new();

    // Vulkan's clip space points down where a System.Numerics projection's points up, which the
    // camera's projection turns as well.
    private static readonly Matrix4x4 RlClipFlip = Matrix4x4.CreateScale(1, -1, 1);

    /// <summary>
    /// Has the matrix calls after this change <paramref name="mode"/>'s matrix, rlgl's modelview,
    /// which moves what is drawn, or its projection, as rlgl's <c>rlMatrixMode</c> does.
    /// </summary>
    /// <remarks>
    /// The projection moves the shapes, text and rlgl's vertices drawn after it. Models are drawn
    /// through the camera of <see cref="BeginMode3D"/>, whatever rlgl's projection is.
    /// </remarks>
    public static void rlMatrixMode(RlMatrixMode mode) => _rlMatrixMode = mode;

    /// <summary>
    /// Keeps the current transform, or the projection in its mode, which <see cref="rlPopMatrix"/>
    /// returns to, as rlgl's <c>rlPushMatrix</c> does. The shapes, text and models drawn until then
    /// are moved by the transforms set in between.
    /// </summary>
    public static void rlPushMatrix()
    {
        if (_rlMatrixMode == RlMatrixMode.Projection) RlProjectionStack.Push(_rlProjection);
        else RlStack.Push(_rlTransform);
    }

    /// <summary>Returns to the transform, or the projection in its mode, the last <see cref="rlPushMatrix"/> kept.</summary>
    public static void rlPopMatrix()
    {
        if (_rlMatrixMode == RlMatrixMode.Projection)
        {
            if (RlProjectionStack.TryPop(out var projection)) SetRlProjection(projection);
        }
        else if (RlStack.TryPop(out var kept)) SetRlTransform(kept);
    }

    /// <summary>Moves what is drawn after by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before, as rlgl's <c>rlTranslatef</c> does.</summary>
    public static void rlTranslatef(float x, float y, float z) => MultiplyRlMatrix(Matrix4x4.CreateTranslation(x, y, z));

    /// <summary>Turns what is drawn after by <paramref name="angle"/> degrees about the axis (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before.</summary>
    public static void rlRotatef(float angle, float x, float y, float z)
    {
        var axis = new Vector3(x, y, z);
        if (axis.LengthSquared() == 0) return;
        MultiplyRlMatrix(Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), float.DegreesToRadians(angle)));
    }

    /// <summary>Scales what is drawn after by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), within the transforms set before.</summary>
    public static void rlScalef(float x, float y, float z) => MultiplyRlMatrix(Matrix4x4.CreateScale(x, y, z));

    /// <summary>
    /// Makes the modelview, the view with the transforms after it, or the projection in its mode,
    /// the identity, as rlgl's <c>rlLoadIdentity</c> does.
    /// </summary>
    public static void rlLoadIdentity()
    {
        if (_rlMatrixMode == RlMatrixMode.Projection)
        {
            SetRlProjection(RlClipFlip);
            return;
        }
        SetRlTransform(Matrix4x4.Identity);
        _rlView = Matrix4x4.Identity;
        DrawList.SetTransform(_rlProjection, DrawList.DepthTest);
    }

    /// <summary>
    /// Multiplies the matrix of the mode in by sixteen values, in raymath's order, as rlgl's
    /// <c>rlMultMatrixf</c> does, what is drawn after moved by them first.
    /// </summary>
    /// <remarks>raymath's order is a <see cref="Matrix4x4"/>'s fields from <c>M11</c> to <c>M44</c> a row at a time.</remarks>
    /// <exception cref="ArgumentException">There are fewer than sixteen values.</exception>
    public static void rlMultMatrixf(ReadOnlySpan<float> matf)
    {
        if (matf.Length < 16) throw new ArgumentException("A matrix is sixteen values.", nameof(matf));
        MultiplyRlMatrix(new Matrix4x4(matf[0], matf[1], matf[2], matf[3], matf[4], matf[5], matf[6], matf[7],
            matf[8], matf[9], matf[10], matf[11], matf[12], matf[13], matf[14], matf[15]));
    }

    /// <summary>
    /// Sets the projection what is drawn after is seen through, as rlgl's <c>rlSetMatrixProjection</c>
    /// does, a projection as <see cref="Matrix4x4.CreatePerspectiveOffCenter(float, float, float, float, float, float)"/>
    /// makes one, depth from 0 to 1.
    /// </summary>
    public static void rlSetMatrixProjection(Matrix4x4 proj) => SetRlProjection(proj * RlClipFlip);

    /// <summary>Tests what is drawn after against the depth drawn before, as rlgl's <c>rlEnableDepthTest</c> does, in 2D as well as 3D.</summary>
    /// <remarks><see cref="BeginMode3D"/> turns the test on and <see cref="EndMode3D"/> turns it off, as raylib's do.</remarks>
    public static void rlEnableDepthTest() => DrawList.SetDepthTest(true);

    /// <summary>Draws what follows over what is there whatever its depth, as rlgl's <c>rlDisableDepthTest</c> does.</summary>
    public static void rlDisableDepthTest() => DrawList.SetDepthTest(false);

    /// <summary>Writes the depth of what is drawn after with the test on, as rlgl's <c>rlEnableDepthMask</c> does, and as the engine does until it is turned off.</summary>
    public static void rlEnableDepthMask()
    {
        _rlDepthMask = true;
        DrawList.SetDepthMask(true);
    }

    /// <summary>
    /// Tests what is drawn after against the depth there without writing its own, as rlgl's
    /// <c>rlDisableDepthMask</c> does, for shapes, text and rlgl's vertices.
    /// </summary>
    public static void rlDisableDepthMask()
    {
        _rlDepthMask = false;
        DrawList.SetDepthMask(false);
    }

    // Whether depth tested shapes write their depth, kept from frame to frame as rlgl's is.
    private static bool _rlDepthMask = true;

    // Changes the matrix rlMatrixMode chose by m, which moves what is drawn before what it held, as
    // rlgl multiplies it in.
    private static void MultiplyRlMatrix(Matrix4x4 m)
    {
        if (_rlMatrixMode == RlMatrixMode.Projection) SetRlProjection(m * _rlProjection);
        else SetRlTransform(m * _rlTransform);
    }

    // Sets the view and the projection a camera mode draws through, and whether it is depth
    // tested, as raylib's modes load rlgl's modelview and projection.
    private static void SetRlCamera(Matrix4x4 view, Matrix4x4 projection, bool depthTest)
    {
        (_rlView, _rlProjection) = (view, projection);
        DrawList.SetTransform(view * projection, depthTest);
    }

    private static void SetRlProjection(Matrix4x4 projection)
    {
        _rlProjection = projection;
        DrawList.SetTransform(_rlView * projection, DrawList.DepthTest);
    }

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

    // Whether what is drawn is blended, as OpenGL's GL_BLEND, kept from frame to frame as rlgl's
    // state is.
    private static bool _rlColorBlend = true;

    /// <summary>Blends what is drawn after by the blend mode again, as rlgl's <c>rlEnableColorBlend</c> does.</summary>
    public static void rlEnableColorBlend()
    {
        _rlColorBlend = true;
        DrawList.SetColorBlend(true);
    }

    /// <summary>
    /// Writes what is drawn after as it is, its alpha with it, shapes and models alike, as rlgl's
    /// <c>rlDisableColorBlend</c> does, so a texture of a G-buffer keeps in its alpha what a
    /// shader puts there.
    /// </summary>
    public static void rlDisableColorBlend()
    {
        _rlColorBlend = false;
        DrawList.SetColorBlend(false);
    }

    // The faces the draw list leaves out of shapes, none until a program turns culling on,
    // whether depth tested shapes write their depth, and whether shapes are blended.
    private static void ApplyRlCulling()
    {
        DrawList.SetCull(_rlCulling == RlCulling.Enabled ? (_rlCullFace == RlCullFace.Front ? CullMode.Front : CullMode.Back) : CullMode.None);
        DrawList.SetDepthMask(_rlDepthMask);
        DrawList.SetColorBlend(_rlColorBlend);
    }

    // Whether a program has set rlgl's culling, after which a model's faces are culled as rlgl
    // culls them, whatever its material says, as raylib's are.
    private static bool RlCullingSet => _rlCulling != RlCulling.Unset || _rlCullFace != RlCullFace.Back;

    // A model's draw with the faces rlgl's culling leaves out, once a program has set it, drawn as
    // points in point mode, and written as it is with blending off.
    private static ModelDraw WithRlState(ModelDraw draw) =>
        !RlCullingSet && !_rlPointMode && _rlColorBlend
            ? draw
            : draw with
            {
                DoubleSided = RlCullingSet ? _rlCulling == RlCulling.Disabled : draw.DoubleSided,
                CullFront = _rlCullFace == RlCullFace.Front,
                Points = _rlPointMode,
                ColorBlend = _rlColorBlend,
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
        RlProjectionStack.Clear();
        _rlMatrixMode = RlMatrixMode.Modelview;
        SetRlTransform(Matrix4x4.Identity);
        (_rlMode, _rlCornerCount, _rlColor, _rlTexCoord, _rlTexture) = (null, 0, Color.White, Vector2.Zero, 0);
        ApplyRlCulling();
    }

    // A window closed takes rlgl's state with it, so the next starts as the engine leaves faces.
    internal static void ForgetRlgl()
    {
        (_rlCulling, _rlCullFace, _rlPointMode) = (RlCulling.Unset, RlCullFace.Back, false);
        (_rlBlendFactors, _rlBlendFactorsSeparate) = (BlendFactors.Default, BlendFactors.Default);
        (_rlDepthMask, _rlColorBlend) = (true, true);
        RlProjectionStack.Clear();
        _rlMatrixMode = RlMatrixMode.Modelview;
        (_rlView, _rlProjection) = (Matrix4x4.Identity, Matrix4x4.Identity);
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
