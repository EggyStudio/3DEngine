using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// The winding of every triangle the shape functions and text record, which raylib's rshapes.c
/// keeps counterclockwise on the screen in 2D and counterclockwise from outside a solid in 3D, so
/// that with rlEnableBackfaceCulling on a program sees here what raylib shows. Read back from the
/// draw list in an app whose stores queue without a GPU.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class WindingTests : IDisposable
{
    private readonly App _app = new();
    private static readonly Texture2D Texture = new(1, 30, 30);

    public WindingTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<DrawList>();
        UseApp(_app);
        ResetRlgl();
    }

    public void Dispose()
    {
        ForgetRlgl();
        UseApp(null);
    }

    // Each triangle a draw records, its corners as the batch's transform puts them on the screen
    // (x right and y down, as Vulkan's clip space runs) for 2D, or in the world for 3D.
    private List<(Vector3 A, Vector3 B, Vector3 C)> Triangles(Action draw, bool onScreen)
    {
        var list = _app.World.Resource<DrawList>();
        list.Clear();
        draw();
        var vertices = list.Vertices.ToArray();
        var indices = list.Indices.ToArray();
        var triangles = new List<(Vector3, Vector3, Vector3)>();
        foreach (var batch in list.Batches)
        {
            if (batch.Topology != PrimitiveTopology.TriangleList) continue;
            for (int i = batch.FirstIndex; i < batch.FirstIndex + batch.IndexCount; i += 3)
            {
                Vector3 Corner(int k)
                {
                    var p = vertices[indices[i + k]].Position;
                    if (!onScreen) return p;
                    var clip = Vector4.Transform(new Vector4(p, 1), batch.Transform);
                    return new Vector3(clip.X / clip.W, clip.Y / clip.W, 0);
                }
                triangles.Add((Corner(0), Corner(1), Corner(2)));
            }
        }
        return triangles;
    }

    // Twice the signed area of a triangle on the screen, below zero where it is counterclockwise
    // there, y running down.
    private static float Area((Vector3 A, Vector3 B, Vector3 C) t) =>
        (t.B.X - t.A.X) * (t.C.Y - t.A.Y) - (t.C.X - t.A.X) * (t.B.Y - t.A.Y);

    public static TheoryData<string> Shapes2D() =>
    [
        "DrawTriangle", "DrawTriangleGradient", "DrawTriangleFan", "DrawTriangleStrip", "DrawTriangleLinesEx",
        "DrawRectangle", "DrawRectanglePro", "DrawRectangleGradientEx", "DrawRectangleLinesEx", "DrawRectangleRounded",
        "DrawRectangleRoundedLinesEx", "DrawCircle", "DrawCircleSector", "DrawCircleSectorReversed", "DrawCircleGradient",
        "DrawCircleLinesEx", "DrawCircleSectorLinesEx", "DrawEllipse", "DrawEllipseV", "DrawEllipseLinesEx", "DrawRing", "DrawRingReversed", "DrawRingLinesEx",
        "DrawPoly", "DrawPolyLinesEx", "DrawLineEx", "DrawLineBezier", "DrawSplineLinear", "DrawSplineBasis",
        "DrawSplineCatmullRom", "DrawSplineBezierQuadratic", "DrawSplineBezierCubic", "DrawPixel",
        "DrawRectangleGradientV", "DrawTexture", "DrawTextureEx", "DrawTextureRec", "DrawTexturePro", "DrawTextureProFlipped", "DrawTextureNPatch",
        "DrawText", "DrawTextEx", "DrawTextPro",
    ];

    private static readonly Dictionary<string, Action> Draw2D = new()
    {
        // Counterclockwise on the screen, as raylib's DrawTriangle asks
        ["DrawTriangle"] = () => DrawTriangle(new(400, 80), new(340, 150), new(460, 150), Color.Red),
        ["DrawTriangleGradient"] = () => DrawTriangleGradient(new(400, 80), new(340, 150), new(460, 150), Color.Red, Color.Green, Color.Blue),
        // The middle first and the rest counterclockwise round it
        ["DrawTriangleFan"] = () => DrawTriangleFan([new(100, 100), new(150, 100), new(100, 50), new(50, 100), new(100, 150)], Color.Red),
        // Top left, bottom left, top right, bottom right, which raylib's strip takes counterclockwise
        ["DrawTriangleStrip"] = () => DrawTriangleStrip([new(0, 0), new(0, 10), new(10, 0), new(10, 10), new(20, 0), new(20, 10)], Color.Red),
        ["DrawTriangleLinesEx"] = () => DrawTriangleLinesEx(new(400, 80), new(340, 150), new(460, 150), 4, Color.Red),
        ["DrawRectangle"] = () => DrawRectangle(10, 20, 100, 50, Color.Red),
        ["DrawRectanglePro"] = () => DrawRectanglePro(new Rectangle(100, 100, 80, 40), new Vector2(40, 20), 30, Color.Red),
        ["DrawRectangleGradientEx"] = () => DrawRectangleGradientEx(new Rectangle(10, 20, 100, 50), Color.Red, Color.Green, Color.Blue, Color.Gold),
        ["DrawRectangleLinesEx"] = () => DrawRectangleLinesEx(new Rectangle(10, 20, 100, 50), 5, Color.Red),
        ["DrawRectangleRounded"] = () => DrawRectangleRounded(new Rectangle(10, 20, 100, 50), 0.5f, 8, Color.Red),
        ["DrawRectangleRoundedLinesEx"] = () => DrawRectangleRoundedLinesEx(new Rectangle(10, 20, 100, 50), 0.5f, 8, 4, Color.Red),
        ["DrawCircle"] = () => DrawCircle(100, 100, 40, Color.Red),
        ["DrawCircleSector"] = () => DrawCircleSector(new Vector2(100, 100), 40, 0, 270, 12, Color.Red),
        ["DrawCircleSectorReversed"] = () => DrawCircleSector(new Vector2(100, 100), 40, 270, 0, 12, Color.Red),
        ["DrawCircleGradient"] = () => DrawCircleGradient(new Vector2(100, 100), 40, Color.Red, Color.Blue),
        ["DrawCircleLinesEx"] = () => DrawCircleLinesEx(new Vector2(100, 100), 40, 4, Color.Red),
        ["DrawCircleSectorLinesEx"] = () => DrawCircleSectorLinesEx(new Vector2(100, 100), 40, 0, 270, 12, 4, Color.Red),
        ["DrawEllipse"] = () => DrawEllipse(100, 100, 60, 30, Color.Red),
        ["DrawEllipseV"] = () => DrawEllipseV(new Vector2(100, 100), 60, 30, Color.Red),
        ["DrawEllipseLinesEx"] = () => DrawEllipseLinesEx(new Vector2(100, 100), 60, 30, 4, Color.Red),
        ["DrawRing"] = () => DrawRing(new Vector2(100, 100), 20, 40, 0, 270, 12, Color.Red),
        ["DrawRingReversed"] = () => DrawRing(new Vector2(100, 100), 20, 40, 270, 0, 12, Color.Red),
        ["DrawRingLinesEx"] = () => DrawRingLinesEx(new Vector2(100, 100), 20, 40, 0, 270, 12, 3, Color.Red),
        ["DrawPoly"] = () => DrawPoly(new Vector2(100, 100), 6, 40, 15, Color.Red),
        ["DrawPolyLinesEx"] = () => DrawPolyLinesEx(new Vector2(100, 100), 6, 40, 15, 4, Color.Red),
        ["DrawLineEx"] = () => DrawLineEx(new Vector2(10, 10), new Vector2(200, 80), 6, Color.Red),
        ["DrawLineBezier"] = () => DrawLineBezier(new Vector2(10, 10), new Vector2(200, 80), 6, Color.Red),
        ["DrawSplineLinear"] = () => DrawSplineLinear([new(10, 10), new(100, 80), new(200, 20)], 6, Color.Red),
        ["DrawSplineBasis"] = () => DrawSplineBasis([new(10, 10), new(100, 80), new(200, 20), new(260, 90)], 6, Color.Red),
        ["DrawSplineCatmullRom"] = () => DrawSplineCatmullRom([new(10, 10), new(100, 80), new(200, 20), new(260, 90)], 6, Color.Red),
        ["DrawSplineBezierQuadratic"] = () => DrawSplineBezierQuadratic([new(10, 10), new(100, 80), new(200, 20)], 6, Color.Red),
        ["DrawSplineBezierCubic"] = () => DrawSplineBezierCubic([new(10, 10), new(60, 80), new(150, 0), new(200, 20)], 6, Color.Red),
        ["DrawPixel"] = () => DrawPixel(10, 10, Color.Red),
        ["DrawRectangleGradientV"] = () => DrawRectangleGradientV(10, 20, 100, 50, Color.Red, Color.Blue),
        ["DrawTexture"] = () => DrawTexture(Texture, 10, 10, Color.White),
        ["DrawTextureEx"] = () => DrawTextureEx(Texture, new Vector2(10, 10), 45, 2, Color.White),
        ["DrawTextureRec"] = () => DrawTextureRec(Texture, new Rectangle(0, 0, -30, 30), new Vector2(10, 10), Color.White),
        ["DrawTexturePro"] = () => DrawTexturePro(Texture, new Rectangle(0, 0, 30, 30), new Rectangle(50, 50, 60, 60), new Vector2(30, 30), 20, Color.White),
        // A source of negative size turns the picture, and raylib turns it by its texture coordinates
        ["DrawTextureProFlipped"] = () => DrawTexturePro(Texture, new Rectangle(0, 0, -30, -30), new Rectangle(50, 50, 60, 60), Vector2.Zero, 0, Color.White),
        ["DrawTextureNPatch"] = () => DrawTextureNPatch(Texture, new NPatchInfo(new Rectangle(0, 0, 30, 30), 10, 10, 10, 10), new Rectangle(10, 10, 100, 60), Vector2.Zero, 0, Color.White),
        ["DrawText"] = () => DrawText("Hello, world", 10, 10, 20, Color.Black),
        ["DrawTextEx"] = () => DrawTextEx(GetFontDefault(), "Hello, world", new Vector2(10, 10), 26, 1, Color.Black),
        ["DrawTextPro"] = () => DrawTextPro(GetFontDefault(), "Hello, world", new Vector2(100, 100), Vector2.Zero, 30, 20, 1, Color.Black),
    };

    [Theory]
    [MemberData(nameof(Shapes2D))]
    public void A_2D_Shape_And_Text_Are_Counterclockwise_On_The_Screen_As_Raylibs_Are(string shape)
    {
        var triangles = Triangles(Draw2D[shape], onScreen: true);

        triangles.Should().NotBeEmpty($"{shape} draws triangles");
        triangles.Count(t => Area(t) > 1e-6f).Should().Be(0,
            $"every one of the {triangles.Count} triangles {shape} draws faces the viewer, so culling the back faces keeps it");
    }

    public static TheoryData<string> Solids3D() =>
    [
        "DrawCube", "DrawSphere", "DrawSphereEx", "DrawCylinder", "DrawCone", "DrawCylinderEx", "DrawCapsule",
    ];

    // Each solid around the origin, whose faces are to face out of it.
    private static readonly Dictionary<string, Action> DrawSolid = new()
    {
        ["DrawCube"] = () => DrawCube(Vector3.Zero, 2, 3, 4, Color.Red),
        ["DrawSphere"] = () => DrawSphere(Vector3.Zero, 2, Color.Red),
        ["DrawSphereEx"] = () => DrawSphereEx(Vector3.Zero, 2, 8, 10, Color.Red),
        ["DrawCylinder"] = () => DrawCylinder(new Vector3(0, -1, 0), 1, 1.5f, 2, 12, Color.Red),
        ["DrawCone"] = () => DrawCylinder(new Vector3(0, -1, 0), 0, 1.5f, 2, 12, Color.Red),
        ["DrawCylinderEx"] = () => DrawCylinderEx(new Vector3(-1, -1, 0), new Vector3(1, 1, 0), 1, 0.5f, 12, Color.Red),
        ["DrawCapsule"] = () => DrawCapsule(new Vector3(0, -1, 0), new Vector3(0, 1, 0), 0.75f, 6, 12, Color.Red),
    };

    [Theory]
    [MemberData(nameof(Solids3D))]
    public void A_3D_Solid_Is_Counterclockwise_From_Outside_As_Raylibs_Is(string shape)
    {
        var triangles = Triangles(DrawSolid[shape], onScreen: false);

        triangles.Should().NotBeEmpty();
        triangles.Count(t => Vector3.Dot(Vector3.Cross(t.B - t.A, t.C - t.A), (t.A + t.B + t.C) / 3) < -1e-6f).Should().Be(0,
            $"every one of the {triangles.Count} faces of {shape} faces out of it");
    }

    [Fact]
    public void A_Plane_Faces_Up_And_A_Triangle_And_Strip_In_3D_Keep_Raylibs_Winding()
    {
        Triangles(() => DrawPlane(Vector3.Zero, new Vector2(2, 3), Color.Red), onScreen: false)
            .Should().OnlyContain(t => Vector3.Cross(t.B - t.A, t.C - t.A).Y > 0, "raylib's plane faces up");

        Triangles(() => DrawTriangle3D(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red), onScreen: false)
            .Should().ContainSingle().Which.Should().Be((Vector3.Zero, Vector3.UnitX, Vector3.UnitY), "a triangle is drawn as given");

        // raylib's strip turns every other triangle round, so all face the same way as the first.
        Vector3[] strip = [new(0, 0, 0), new(0, -1, 0), new(1, 0, 0), new(1, -1, 0), new(2, 0, 0)];
        Triangles(() => DrawTriangleStrip3D(strip, Color.Red), onScreen: false)
            .Should().OnlyContain(t => Vector3.Cross(t.B - t.A, t.C - t.A).Z > 0);
    }

    [Fact]
    public void A_Billboard_Faces_The_Camera()
    {
        var camera = new Camera3D(new Vector3(3, 4, 10), Vector3.Zero, Vector3.UnitY);
        foreach (var draw in new Action[]
        {
            () => DrawBillboard(camera, Texture, new Vector3(1, 1, 0), 2, Color.White),
            () => DrawBillboardRec(camera, Texture, new Rectangle(0, 0, 30, 30), new Vector3(1, 1, 0), new Vector2(2, 1), Color.White),
            () => DrawBillboardPro(camera, Texture, new Rectangle(0, 0, 30, 30), new Vector3(1, 1, 0), Vector3.UnitY, new Vector2(2, 1), Vector2.Zero, 30, Color.White),
        })
        {
            Triangles(draw, onScreen: false).Should().OnlyContain(t =>
                Vector3.Dot(Vector3.Cross(t.B - t.A, t.C - t.A), camera.Position - (t.A + t.B + t.C) / 3) > 0);
        }
    }
}
