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

    public void Dispose() => UseApp(null);

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
}
