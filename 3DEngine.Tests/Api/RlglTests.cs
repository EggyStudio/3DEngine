using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// rlgl's vertices given one at a time and its matrix stack, read back as what they record into
/// the draw list, in an app whose stores queue without a GPU.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class RlglTests : IDisposable
{
    private readonly App _app = new();

    public RlglTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        UseApp(_app);
        // rlgl's state is the flat API's, so each test starts from none pushed.
        ResetRlgl();
    }

    public void Dispose()
    {
        // Culling and point mode outlast a frame, as rlgl's do, so they are forgotten for the next test.
        ForgetRlgl();
        UseApp(null);
    }

    private DrawList List => _app.World.Resource<DrawList>();
    private ReadOnlySpan<ImmediateVertex> Vertices => List.Vertices;

    [Fact]
    public void A_Quad_Of_Vertices_Is_Two_Triangles_Each_Corner_With_Its_Own_Color_And_Coordinate()
    {
        rlSetTexture(7);
        rlBegin(RlDrawMode.Quads);
            rlColor4ub(255, 0, 0, 255); rlTexCoord2f(0, 0); rlVertex2f(10, 10);
            rlColor4ub(0, 255, 0, 255); rlTexCoord2f(0, 1); rlVertex2f(10, 20);
            rlColor4ub(0, 0, 255, 255); rlTexCoord2f(1, 1); rlVertex2f(20, 20);
            rlColor4f(1, 1, 1, 0.5f); rlTexCoord2f(1, 0); rlVertex2f(20, 10);
        rlEnd();
        rlSetTexture(0);

        Vertices.ToArray().Should().Equal(
            new ImmediateVertex(new Vector3(10, 10, 0), new Vector2(0, 0), new Color(255, 0, 0)),
            new ImmediateVertex(new Vector3(10, 20, 0), new Vector2(0, 1), new Color(0, 255, 0)),
            new ImmediateVertex(new Vector3(20, 20, 0), new Vector2(1, 1), new Color(0, 0, 255)),
            new ImmediateVertex(new Vector3(20, 10, 0), new Vector2(1, 0), new Color(255, 255, 255, 127)));
        List.Indices.ToArray().Should().Equal(0u, 1u, 2u, 0u, 2u, 3u);
        List.Batches.Should().ContainSingle().Which.Texture.Should().Be(7, "rlSetTexture names the quads' texture");
    }

    [Fact]
    public void Lines_And_Triangles_Take_Their_Vertices_Two_And_Three_At_A_Time_And_An_Unfinished_One_Draws_Nothing()
    {
        rlBegin(RlDrawMode.Lines);
            rlVertex2f(0, 0); rlVertex2f(5, 0);
            rlVertex2f(9, 9);
        rlEnd();
        rlBegin(RlDrawMode.Triangles);
            rlVertex2f(0, 0); rlVertex2f(1, 0); rlVertex2f(0, 1);
        rlEnd();

        Vertices.Length.Should().Be(5, "a line of two, a triangle of three, and the third line vertex left out");
        List.Batches.Select(b => b.Topology).Should().Equal(PrimitiveTopology.LineList, PrimitiveTopology.TriangleList);
    }

    [Fact]
    public void The_Matrix_Stack_Moves_Vertices_And_Shapes_Until_Its_Pop()
    {
        rlPushMatrix();
            rlTranslatef(10, 0, 0);
            rlRotatef(90, 0, 0, 1);
            rlScalef(2, 2, 2);
            rlBegin(RlDrawMode.Lines);
                rlVertex3f(1, 0, 0); rlVertex3f(0, 0, 0);
            rlEnd();
            DrawLine3D(new Vector3(0, 1, 0), Vector3.Zero, Color.Red);
        rlPopMatrix();
        DrawLine3D(new Vector3(0, 1, 0), Vector3.Zero, Color.Red);

        var positions = Vertices.ToArray().Select(v => v.Position).ToArray();
        Vector3.Distance(positions[0], new Vector3(10, 2, 0)).Should().BeLessThan(1e-5f,
            "scaled by two, turned a quarter about Z, then moved ten along X, as rlgl applies them, the last given first");
        positions[1].Should().Be(new Vector3(10, 0, 0));
        positions[2].X.Should().BeApproximately(8, 1e-5f, "a shape inside the push is moved as the vertices are");
        positions[4].Should().Be(new Vector3(0, 1, 0), "after the pop nothing is moved");
    }

    [Fact]
    public void A_Model_Drawn_Inside_A_Push_Is_Moved_As_Raylibs_DrawMesh_Moves_It()
    {
        _app.World.InitResource<ModelDrawList>();
        var mesh = new ModelMesh(1, 3, 1, default);

        rlPushMatrix();
            rlTranslatef(0, 5, 0);
            DrawMesh(mesh, new ModelMaterial(Color.White), Matrix4x4.CreateTranslation(1, 0, 0));
        rlPopMatrix();

        _app.World.Resource<ModelDrawList>().Draws.Should().ContainSingle().Which.World.Translation.Should().Be(new Vector3(1, 5, 0));
    }

    [Fact]
    public void Shapes_Draw_Both_Faces_Until_Culling_Is_Turned_On_And_Then_Leave_Out_The_Faces_Set()
    {
        DrawRectangle(0, 0, 4, 4, Color.White);
        rlEnableBackfaceCulling();
        DrawRectangle(0, 0, 4, 4, Color.White);
        rlSetCullFace(RlCullFace.Front);
        DrawRectangle(0, 0, 4, 4, Color.White);
        rlDisableBackfaceCulling();
        DrawRectangle(0, 0, 4, 4, Color.White);

        List.Batches.Select(b => b.Cull).Should().Equal(CullMode.None, CullMode.Back, CullMode.Front, CullMode.None);
    }

    [Fact]
    public void Culling_Outlasts_The_Frame_It_Was_Set_In()
    {
        rlEnableBackfaceCulling();
        List.Clear();
        ResetRlgl();
        DrawRectangle(0, 0, 4, 4, Color.White);

        List.Batches.Should().ContainSingle().Which.Cull.Should().Be(CullMode.Back, "rlgl's state is kept from frame to frame");
    }

    [Fact]
    public void A_Model_Keeps_Its_Materials_Faces_Until_Culling_Is_Set_And_Point_Mode_Draws_Its_Corners()
    {
        _app.World.InitResource<ModelDrawList>();
        var mesh = new ModelMesh(1, 3, 1, default);
        var draws = _app.World.Resource<ModelDrawList>();
        void Draw(bool doubleSided = false) =>
            DrawMesh(mesh, new ModelMaterial(Color.White) { DoubleSided = doubleSided }, Matrix4x4.Identity);

        Draw();
        Draw(doubleSided: true);
        rlEnablePointMode();
        Draw(doubleSided: true);
        rlDisablePointMode();
        rlSetCullFace(RlCullFace.Front);
        Draw(doubleSided: true);
        rlDisableBackfaceCulling();
        Draw();
        rlEnableBackfaceCulling();
        rlSetCullFace(RlCullFace.Back);
        Draw(doubleSided: true);

        draws.Draws.ToArray().Select(d => (d.DoubleSided, d.CullFront, d.Points)).Should().Equal(
            [(false, false, false), (true, false, false), (true, false, true), (false, true, false), (true, true, false), (false, false, false)],
            "a model keeps its material's faces until rlgl's culling is set, and then follows it");
    }

    [Fact]
    public void A_Custom_Blend_Mode_Takes_The_Factors_Last_Set_And_The_Other_Modes_None()
    {
        rlSetBlendFactors(RlBlendFactor.SrcAlpha, RlBlendFactor.SrcAlpha, RlBlendEquation.Min);
        BeginBlendMode(BlendMode.Custom);
        DrawRectangle(0, 0, 4, 4, Color.White);
        // Set while the mode is on, as textures_magnifying_glass sets its own
        rlSetBlendFactors(RlBlendFactor.One, RlBlendFactor.One, RlBlendEquation.Max);
        DrawRectangle(0, 0, 4, 4, Color.White);
        BeginBlendMode(BlendMode.CustomSeparate);
        rlSetBlendFactorsSeparate(RlBlendFactor.Zero, RlBlendFactor.One, RlBlendFactor.One, RlBlendFactor.Zero, RlBlendEquation.FuncAdd, RlBlendEquation.FuncAdd);
        DrawRectangle(0, 0, 4, 4, Color.White);
        rlSetBlendMode(BlendMode.Additive);
        DrawRectangle(0, 0, 4, 4, Color.White);
        EndBlendMode();

        List.Batches.Select(b => (b.Blend, b.Factors)).Should().Equal(
            (BlendMode.Custom, new BlendFactors(RlBlendFactor.SrcAlpha, RlBlendFactor.SrcAlpha, RlBlendEquation.Min, RlBlendFactor.SrcAlpha, RlBlendFactor.SrcAlpha, RlBlendEquation.Min)),
            (BlendMode.Custom, new BlendFactors(RlBlendFactor.One, RlBlendFactor.One, RlBlendEquation.Max, RlBlendFactor.One, RlBlendFactor.One, RlBlendEquation.Max)),
            (BlendMode.CustomSeparate, new BlendFactors(RlBlendFactor.Zero, RlBlendFactor.One, RlBlendEquation.FuncAdd, RlBlendFactor.One, RlBlendFactor.Zero, RlBlendEquation.FuncAdd)),
            (BlendMode.Additive, default(BlendFactors)));
    }
}
