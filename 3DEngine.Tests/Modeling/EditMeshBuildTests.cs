using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// An edit mesh built into what the engine draws and collides with, a mesh of each slot with each
/// face's own corners, normals and texture coordinates in meters, smoothing within an angle, concave
/// faces cut inside their outlines, the meshes written again in place after a change, and a collider
/// a ball lands on.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class EditMeshBuildTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly List<EditMesh> _made = [];

    public EditMeshBuildTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
        CloseWindow();
        UseApp(null);
    }

    private EditMesh New()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        return mesh;
    }

    private static (ModelVertex[] Vertices, uint[] Indices)[] Built(EditMesh mesh)
    {
        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();
        return EditMeshBuild.Slots(data);
    }

    [Fact]
    public void A_Box_Builds_Into_One_Mesh_Of_Twenty_Four_Corners_Each_Face_With_Its_Own_Normal()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        var (vertices, indices) = Built(mesh).Single();
        (vertices.Length, indices.Length).Should().Be((24, 36));

        // Each corner of the top faces up and is laid out by X and Z, in meters, so the top's texture
        // spans two by four.
        var top = vertices.Where(vertex => vertex.Normal == Vector3.UnitY).ToArray();
        top.Should().HaveCount(4).And.OnlyContain(vertex => vertex.Position.Y == 3);
        (top.Max(vertex => vertex.Uv.X) - top.Min(vertex => vertex.Uv.X)).Should().BeApproximately(2, Close);
        (top.Max(vertex => vertex.Uv.Y) - top.Min(vertex => vertex.Uv.Y)).Should().BeApproximately(4, Close);

        // A wall's second coordinate runs down the world, so a texture stands upright on it.
        var front = vertices.Where(vertex => vertex.Normal == Vector3.UnitZ).ToArray();
        (front.MinBy(vertex => vertex.Position.Y).Uv.Y - front.MaxBy(vertex => vertex.Position.Y).Uv.Y).Should().BeApproximately(3, Close);

        AllTrianglesFaceTheirNormals(vertices, indices);
    }

    [Fact]
    public void Faces_In_Slots_Build_Into_A_Mesh_Each_In_The_Order_Of_The_Slots()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(4, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "ceiling");
        EditMeshSelectAll(mesh);
        EditMeshMaterial(mesh, "wall");
        var built = Built(mesh);

        GetEditMeshSlots(mesh).Should().Equal("default", "ceiling", "wall");
        built.Should().HaveCount(3);
        // A slot no face is in is one triangle with no area.
        built[0].Indices.Should().Equal(0u, 1u, 2u);
        built[0].Vertices.Select(vertex => vertex.Position).Distinct().Should().ContainSingle();
        (built[2].Vertices.Length, built[2].Indices.Length).Should().Be((24, 36));

        EditMeshSelectFacing(mesh, -Vector3.UnitY);
        EditMeshMaterial(mesh, "floor");
        built = Built(mesh);
        (built[2].Vertices.Length, built[3].Vertices.Length).Should().Be((20, 4));
    }

    [Fact]
    public void A_Cylinder_Smoothed_Past_The_Angle_Between_Its_Sides_Is_Round_And_Its_Ends_Stay_Flat()
    {
        var mesh = New();
        EditMeshCylinder(mesh, 1, 2, 8, Vector3.Zero, centered: true);
        EditMeshSelectAll(mesh);
        EditMeshShade(mesh, true, 50);

        foreach (var vertex in Built(mesh)[0].Vertices)
        {
            if (MathF.Abs(vertex.Normal.Y) > 0.5f)
            {
                // An end, ninety degrees from the sides, keeps its own normal.
                MathF.Abs(vertex.Normal.Y).Should().BeApproximately(1, Close);
                continue;
            }
            // A side's corner faces straight out from the axis, as a round side's would.
            var outward = Vector3.Normalize(vertex.Position with { Y = 0 });
            vertex.Normal.Y.Should().BeApproximately(0, Close);
            Vector3.Dot(vertex.Normal, outward).Should().BeGreaterThan(0.9999f, $"{vertex.Normal} at {vertex.Position}");
        }

        // Smoothed at thirty degrees, less than the forty-five between sides, it stays faceted.
        var faceted = New();
        EditMeshCylinder(faceted, 1, 2, 8);
        EditMeshSelectAll(faceted);
        EditMeshShade(faceted, true);
        Built(faceted)[0].Vertices.Select(vertex => vertex.Normal).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public void A_Flights_Concave_Sides_Are_Cut_Inside_Their_Outlines()
    {
        var mesh = New();
        EditMeshStairs(mesh, 6, 0.2f, 0.3f, 1.5f);
        var (vertices, indices) = Built(mesh)[0];

        // Each side of 14 corners is 12 triangles, and the 14 faces of four corners 2 each.
        indices.Length.Should().Be((2 * 12 + 14 * 2) * 3);
        AllTrianglesFaceTheirNormals(vertices, indices);
        Volume(vertices.Select(vertex => vertex.Position).ToArray(), indices).Should().BeApproximately(GetEditMeshInfo(mesh).Volume, Close);
    }

    [Fact]
    public void A_Model_Is_Built_A_Mesh_And_A_Material_A_Slot_And_Written_Again_In_Place_After_A_Change()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        var model = LoadModelFromEditMesh(mesh);

        IsModelValid(model).Should().BeTrue();
        (model.Meshes.Length, model.Materials.Length).Should().Be((1, 1));
        model.MeshMaterial.Should().Equal(0);
        (model.Meshes[0].VertexCount, model.Meshes[0].TriangleCount).Should().Be((24, 12));
        GetModelBoundingBox(model).Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 3, 4)));
        var first = model.Meshes[0].Id;
        model.Materials[0].Color = Color.Red;

        // The top pushed up a meter and put in a slot of its own, the first slot's mesh keeping its
        // id and the material the program gave it.
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshExtrude(mesh, 1);
        EditMeshMaterial(mesh, "roof");
        UpdateModelFromEditMesh(model, mesh);

        model.Meshes.Should().HaveCount(2);
        model.Meshes[0].Id.Should().Be(first);
        model.Materials[model.MeshMaterial[0]].Color.Should().Be(Color.Red);
        model.Materials[model.MeshMaterial[1]].Color.Should().Be(Color.White);
        IsModelValid(model).Should().BeTrue();
        GetModelBoundingBox(model).Max.Y.Should().Be(4);
        // The sides and the bottom, five faces of four corners and four sides, in the first slot.
        model.Meshes[0].TriangleCount.Should().Be((5 + 4) * 2);

        // Cleared, the slot added is taken away with its mesh.
        var roof = model.Meshes[1];
        EditMeshClear(mesh);
        EditMeshBox(mesh, Vector3.One);
        UpdateModelFromEditMesh(model, mesh);
        model.Meshes.Should().ContainSingle().Which.Id.Should().Be(first);
        IsModelValid(LoadModelFromMesh(roof)).Should().BeFalse("the slot's mesh was freed");

        UnloadModel(model);
    }

    [Fact]
    public void A_Ball_Dropped_On_A_Built_Floor_Lands_On_It()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(10, 1, 10), new Vector3(0, 0.5f, 0), centered: true);
        EditMeshMaterial(mesh, "floor");
        var model = LoadModelFromEditMesh(mesh);

        // The default slot has no faces, and its triangle with no area is left out of the collider.
        var floor = CreatePhysicsStaticModel(model, Vector3.Zero);
        floor.IsValid.Should().BeTrue();
        var ball = CreatePhysicsSphere(new Vector3(0, 5, 0), 0.5f);
        for (int frame = 0; frame < 180; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        // On the floor's top at a meter, the ball's middle half a meter above it.
        GetPhysicsBodyPosition(ball).Y.Should().BeApproximately(1.5f, 0.02f);
        UnloadModel(model);
    }

    // Each triangle's winding gives a normal along its corners' normals.
    private static void AllTrianglesFaceTheirNormals(ModelVertex[] vertices, uint[] indices)
    {
        for (int t = 0; t < indices.Length; t += 3)
        {
            var (a, b, c) = (vertices[indices[t]].Position, vertices[indices[t + 1]].Position, vertices[indices[t + 2]].Position);
            Vector3.Dot(Vector3.Cross(b - a, c - a), vertices[indices[t]].Normal).Should().BeGreaterThan(0, "the triangle at {0} winds the way it faces", t / 3);
        }
    }

    // The volume triangles close, by the solid each makes with the origin.
    private static float Volume(Vector3[] positions, uint[] indices)
    {
        double sum = 0;
        for (int t = 0; t < indices.Length; t += 3)
            sum += Vector3.Dot(positions[indices[t]], Vector3.Cross(positions[indices[t + 1]], positions[indices[t + 2]]));
        return (float)(sum / 6.0);
    }
}
