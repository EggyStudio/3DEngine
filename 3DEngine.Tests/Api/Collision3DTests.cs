using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class Collision3DTests
{
    private static readonly Ray Forward = new(new Vector3(0, 0, 5), -Vector3.UnitZ);

    [Fact]
    public void A_Ray_Meets_A_Sphere_At_Its_Near_Side_And_From_Inside_At_Its_Far_One()
    {
        var hit = GetRayCollisionSphere(Forward, Vector3.Zero, 1);
        (hit.Hit, hit.Distance).Should().Be((true, 4f));
        hit.Normal.Should().Be(Vector3.UnitZ);

        GetRayCollisionSphere(new Ray(Vector3.Zero, Vector3.UnitX), Vector3.Zero, 1).Distance.Should().Be(1, "from inside it meets the far side");
        GetRayCollisionSphere(new Ray(new Vector3(0, 2, 5), -Vector3.UnitZ), Vector3.Zero, 1).Hit.Should().BeFalse();
    }

    [Fact]
    public void A_Ray_Meets_A_Box_On_The_Face_It_Comes_At()
    {
        var hit = GetRayCollisionBox(Forward, new BoundingBox(new Vector3(-1), new Vector3(1)));
        (hit.Hit, hit.Distance, hit.Normal).Should().Be((true, 4f, Vector3.UnitZ));
        hit.Point.Should().Be(new Vector3(0, 0, 1));
        GetRayCollisionBox(new Ray(new Vector3(0, 0, 5), Vector3.UnitZ), new BoundingBox(new Vector3(-1), new Vector3(1))).Hit
            .Should().BeFalse("the box is behind the ray");
    }

    [Fact]
    public void A_Ray_Meets_A_Triangle_A_Quad_And_A_Placed_Mesh()
    {
        var triangle = GetRayCollisionTriangle(Forward, new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(0, 1, 0));
        (triangle.Hit, triangle.Distance).Should().Be((true, 5f));
        GetRayCollisionTriangle(new Ray(new Vector3(2, 2, 5), -Vector3.UnitZ), new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(0, 1, 0)).Hit.Should().BeFalse();
        GetRayCollisionQuad(Forward, new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0)).Distance.Should().Be(5);

        var app = new App();
        app.World.InitResource<TextureStore>();
        app.World.InitResource<MeshStore>();
        UseApp(app);
        try
        {
            var cube = GenMeshCube(2, 2, 2);
            GetMeshBoundingBox(cube).Should().Be(new BoundingBox(new Vector3(-1), new Vector3(1)));
            var hit = GetRayCollisionMesh(Forward, cube, Matrix4x4.CreateTranslation(0, 0, -2));
            (hit.Hit, hit.Distance).Should().Be((true, 6f), "the cube moved two back puts its front face at z -1");
        }
        finally
        {
            UseApp(null);
        }
    }

    [Fact]
    public void Spheres_And_Boxes_Overlap_Or_Not()
    {
        CheckCollisionSpheres(Vector3.Zero, 1, new Vector3(1.9f, 0, 0), 1).Should().BeTrue();
        CheckCollisionSpheres(Vector3.Zero, 1, new Vector3(2.1f, 0, 0), 1).Should().BeFalse();
        var box = new BoundingBox(Vector3.Zero, Vector3.One);
        CheckCollisionBoxes(box, new BoundingBox(new Vector3(0.5f), new Vector3(2))).Should().BeTrue();
        CheckCollisionBoxes(box, new BoundingBox(new Vector3(1.1f), new Vector3(2))).Should().BeFalse();
        CheckCollisionBoxSphere(box, new Vector3(1.5f, 0.5f, 0.5f), 0.6f).Should().BeTrue();
        CheckCollisionBoxSphere(box, new Vector3(2, 2, 2), 0.6f).Should().BeFalse();
    }

    [Fact]
    public void Points_Lines_And_Polygons_Collide_In_2D()
    {
        CheckCollisionPointTriangle(new Vector2(1, 1), Vector2.Zero, new Vector2(4, 0), new Vector2(0, 4)).Should().BeTrue();
        CheckCollisionPointTriangle(new Vector2(3, 3), Vector2.Zero, new Vector2(4, 0), new Vector2(0, 4)).Should().BeFalse();
        CheckCollisionPointLine(new Vector2(5, 2), Vector2.Zero, new Vector2(10, 0), 2).Should().BeTrue();
        CheckCollisionPointLine(new Vector2(5, 3), Vector2.Zero, new Vector2(10, 0), 2).Should().BeFalse();
        Vector2[] square = [Vector2.Zero, new(4, 0), new(4, 4), new(0, 4)];
        CheckCollisionPointPoly(new Vector2(2, 2), square).Should().BeTrue();
        CheckCollisionPointPoly(new Vector2(5, 2), square).Should().BeFalse();
        CheckCollisionLines(Vector2.Zero, new Vector2(4, 4), new Vector2(0, 4), new Vector2(4, 0), out var crossing).Should().BeTrue();
        crossing.Should().Be(new Vector2(2, 2));
        CheckCollisionLines(Vector2.Zero, new Vector2(1, 1), new Vector2(0, 4), new Vector2(4, 0), out _).Should().BeFalse();
        CheckCollisionCircleLine(new Vector2(5, 1), 1.5f, Vector2.Zero, new Vector2(10, 0)).Should().BeTrue();
    }

    [Fact]
    public void A_World_Point_Lands_Where_The_Ray_Through_Its_Pixel_Comes_From()
    {
        var camera = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY);
        GetWorldToScreenEx(Vector3.Zero, camera, 800, 450).Should().Be(new Vector2(400, 225));

        var right = GetWorldToScreenEx(new Vector3(1, 0, 0), camera, 800, 450);
        var up = GetWorldToScreenEx(new Vector3(0, 1, 0), camera, 800, 450);
        right.X.Should().BeGreaterThan(400);
        up.Y.Should().BeLessThan(225, "up in the world is up the screen, where pixels count down");

        // The ray through the pixel passes through the point again.
        var point = new Vector3(2, -1, 3);
        var ray = GetScreenToWorldRayEx(GetWorldToScreenEx(point, camera, 800, 450), camera, 800, 450);
        var along = Vector3.Dot(point - ray.Position, ray.Direction);
        Vector3.Distance(ray.Position + ray.Direction * along, point).Should().BeLessThan(1e-3f);

        IsPointInFrontOfCamera(point, camera).Should().BeTrue();
        IsPointInFrontOfCamera(new Vector3(0, 0, 11), camera).Should().BeFalse();
        GetCameraMatrix(camera).Should().Be(camera.View);
    }
}
