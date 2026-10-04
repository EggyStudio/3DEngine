using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Trait("Category", "Unit")]
public class Engine3D2DTests
{
    [Fact]
    public void A_2D_Camera_Puts_Its_Target_At_Its_Offset_And_Converts_Both_Ways()
    {
        var camera = new Camera2D(new Vector2(160, 90), new Vector2(500, 40), Rotation: 0, Zoom: 2);

        GetWorldToScreen2D(new Vector2(500, 40), camera).Should().Be(new Vector2(160, 90));
        GetWorldToScreen2D(new Vector2(510, 40), camera).Should().Be(new Vector2(180, 90), "ten world units at a zoom of 2 are twenty pixels");
        var back = GetScreenToWorld2D(new Vector2(180, 90), camera);
        Vector2.Distance(back, new Vector2(510, 40)).Should().BeLessThan(1e-3f);

        var turned = camera with { Rotation = 90, Zoom = 1 };
        Vector2.Distance(GetWorldToScreen2D(new Vector2(510, 40), turned), new Vector2(160, 100)).Should().BeLessThan(1e-3f,
            "a quarter turn takes a point to the target's right down the screen");
    }

    [Fact]
    public void Rectangles_Circles_And_Points_Collide_As_Raylib_Has_It()
    {
        var a = new Rectangle(0, 0, 10, 10);
        CheckCollisionRecs(a, new Rectangle(9, 9, 5, 5)).Should().BeTrue();
        CheckCollisionRecs(a, new Rectangle(10, 0, 5, 5)).Should().BeFalse("touching edges do not overlap");
        GetCollisionRec(a, new Rectangle(5, 6, 10, 10)).Should().Be(new Rectangle(5, 6, 5, 4));
        GetCollisionRec(a, new Rectangle(20, 20, 1, 1)).Should().Be(default(Rectangle));

        CheckCollisionCircleRec(new Vector2(12, 5), 2.5f, a).Should().BeTrue("the circle reaches the rectangle's right edge");
        CheckCollisionCircleRec(new Vector2(13, 13), 2.5f, a).Should().BeFalse("the corner is farther than its radius");
        CheckCollisionCircles(Vector2.Zero, 1, new Vector2(1.5f, 0), 0.6f).Should().BeTrue();
        CheckCollisionPointRec(new Vector2(5, 5), a).Should().BeTrue();
        CheckCollisionPointCircle(new Vector2(3, 4), Vector2.Zero, 4.9f).Should().BeFalse();
    }

    [Fact]
    public void Text_Saved_Beside_The_Program_Is_Read_Back()
    {
        var name = $"engine-test-{Guid.NewGuid():N}.txt";
        try
        {
            FileExists(name).Should().BeFalse();
            LoadFileText(name).Should().BeNull();
            SaveFileText(name, "42").Should().BeTrue();
            FileExists(name).Should().BeTrue();
            LoadFileText(name).Should().Be("42");
            File.Exists(Path.Combine(GetApplicationDirectory(), name)).Should().BeTrue("a relative name is beside the program");
        }
        finally
        {
            File.Delete(Path.Combine(AppContext.BaseDirectory, name));
        }
    }
}
