using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The modifier stack, each modifier's counts and volume on what the mesh builds into, the mesh
/// itself left as it was shaped, the stack reordered, taken from and applied, its lines in the
/// recipe, and the time a stack takes to build.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModifierTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
    }

    private EditMesh Box(Vector3 size)
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, size);
        return mesh;
    }

    private static (int, int, int) Counts(EditMeshInfo info) => (info.VertexCount, info.EdgeCount, info.FaceCount);

    private static string[] Lines(EditMesh mesh) => GetEditMeshRecipe(mesh).Split('\n', StringSplitOptions.RemoveEmptyEntries)[1..];

    [Fact]
    public void A_Half_Box_With_A_Mirror_Builds_Whole_And_Stays_A_Half()
    {
        var mesh = Box(new Vector3(1, 2, 2));
        EditMeshSelectFacing(mesh, -Vector3.UnitX);
        EditMeshDelete(mesh);
        EditMeshModifierMirror(mesh, "x");

        var built = GetEditMeshModifiedInfo(mesh);
        Counts(built).Should().Be((12, 20, 10));
        built.IsClosed.Should().BeTrue();
        built.Volume.Should().BeApproximately(8, Close);

        // The half itself is as it was, open, and an edit to it is built mirrored.
        Counts(GetEditMeshInfo(mesh)).Should().Be((8, 12, 5));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMove(mesh, new Vector3(0, 1, 0));
        GetEditMeshModifiedInfo(mesh).Volume.Should().BeApproximately(12, Close);
        GetEditMeshModifiers(mesh).Should().Equal("mirror x");
    }

    [Fact]
    public void An_Array_Of_Three_Repeats_The_Mesh_By_Its_Offset_Or_Along_A_Curve()
    {
        var mesh = Box(Vector3.One);
        EditMeshModifierArray(mesh, 3, new Vector3(2, 0, 0));
        var built = GetEditMeshModifiedInfo(mesh);
        Counts(built).Should().Be((24, 36, 18));
        built.Volume.Should().BeApproximately(3, Close);
        built.Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(5, 1, 1)));

        var curved = Box(Vector3.One);
        EditMeshModifierArrayCurve(curved, [new Vector3(1, 0, 0), new Vector3(1, 0, 3), new Vector3(5, 2, 0)]);
        GetEditMeshModifiedInfo(curved).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(5, 3, 4)));
        GetEditMeshModifiedInfo(curved).Volume.Should().BeApproximately(3, Close);
    }

    [Fact]
    public void A_Box_Subdivided_To_Level_One_Has_26_Vertices_And_To_Level_Two_98()
    {
        var mesh = Box(new Vector3(2));
        EditMeshModifierSubdivision(mesh, 1);
        var once = GetEditMeshModifiedInfo(mesh);
        Counts(once).Should().Be((26, 48, 24));
        once.IsClosed.Should().BeTrue();

        EditMeshModifierRemove(mesh, 0);
        EditMeshModifierSubdivision(mesh, 2);
        var twice = GetEditMeshModifiedInfo(mesh);
        Counts(twice).Should().Be((98, 192, 96));
        twice.IsClosed.Should().BeTrue();

        // Each level draws the box in toward a rounded shape, holding less.
        twice.Volume.Should().BeLessThan(once.Volume).And.BeGreaterThan(0);
        once.Volume.Should().BeLessThan(8);
    }

    [Fact]
    public void A_Solidified_Plane_Is_A_Closed_Slab_Out_Or_In()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshPlane(mesh, 2, 2);
        EditMeshModifierSolidify(mesh, 0.1f);

        var outward = GetEditMeshModifiedInfo(mesh);
        Counts(outward).Should().Be((8, 12, 6));
        outward.IsClosed.Should().BeTrue();
        outward.Volume.Should().BeApproximately(0.4f, Close);
        outward.Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 0.1f, 2)));

        EditMeshModifierRemove(mesh, 0);
        EditMeshModifierSolidify(mesh, -0.1f);
        var inward = GetEditMeshModifiedInfo(mesh);
        inward.IsClosed.Should().BeTrue();
        inward.Volume.Should().BeApproximately(0.4f, Close);
        inward.Bounds.Min.Y.Should().BeApproximately(-0.1f, Close);
    }

    [Fact]
    public void A_Bevel_Modifier_Rounds_Every_Edge_Or_Those_Past_An_Angle()
    {
        var every = Box(new Vector3(2));
        EditMeshModifierBevel(every, 0.1f);
        Counts(GetEditMeshModifiedInfo(every)).Should().Be((24, 48, 26));
        GetEditMeshModifiedInfo(every).IsClosed.Should().BeTrue();

        // A top cut in four keeps the edges inside it, which lie between faces in one plane.
        var past = Box(new Vector3(2));
        EditMeshSelectFacing(past, Vector3.UnitY);
        EditMeshSubdivide(past);
        EditMeshModifierBevel(past, 0.1f, 2, 30);
        var built = GetEditMeshModifiedInfo(past);
        built.IsClosed.Should().BeTrue();
        built.Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2)));
        EditMeshStore.TryGet(past, out var data).Should().BeTrue();
        var top = data.Modified();
        Enumerable.Range(0, top.FaceCount).Count(face => top.Normal(face) == Vector3.UnitY).Should().Be(4);
    }

    [Fact]
    public void The_Stack_Applies_In_Order_And_Is_Moved_Taken_From_And_Applied()
    {
        var mesh = Box(new Vector3(2));
        EditMeshModifierTriangulate(mesh);
        EditMeshModifierSubdivision(mesh, 1);
        // Twelve triangles, each three faces of four corners.
        GetEditMeshModifiedInfo(mesh).FaceCount.Should().Be(36);

        EditMeshModifierMove(mesh, 1, 0);
        GetEditMeshModifiers(mesh).Should().Equal("subdivision 1", "triangulate");
        // Twenty-four faces of four corners, each two triangles.
        GetEditMeshModifiedInfo(mesh).FaceCount.Should().Be(48);

        EditMeshModifierRemove(mesh, 0);
        GetEditMeshModifiedInfo(mesh).FaceCount.Should().Be(12);

        EditMeshModifierApply(mesh, 0);
        GetEditMeshModifiers(mesh).Should().BeEmpty();
        Counts(GetEditMeshInfo(mesh)).Should().Be((8, 18, 12));
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(8, Close);

        // The apply is a line of the recipe, so an undo brings the modifier back and the box whole.
        EditMeshUndo(mesh).Should().BeTrue();
        GetEditMeshModifiers(mesh).Should().Equal("triangulate");
        GetEditMeshInfo(mesh).FaceCount.Should().Be(6);
        Lines(mesh).Should().Equal("box 2 2 2", "modifier triangulate", "modifier subdivision 1", "modifier move 1 0", "modifier remove 0");
    }

    [Fact]
    public void A_Stack_Read_Back_From_The_Recipe_Builds_The_Same()
    {
        var mesh = Box(new Vector3(1, 2, 2));
        EditMeshSelectFacing(mesh, -Vector3.UnitX);
        EditMeshDelete(mesh);
        EditMeshModifierMirror(mesh, "xz", new Vector3(0, 0, -1));
        EditMeshModifierArray(mesh, 2, new Vector3(0, 3, 0));
        EditMeshModifierArrayCurve(mesh, [Vector3.Zero, new Vector3(5, 0, 0)]);
        EditMeshModifierBevel(mesh, 0.05f, 2, 45);
        EditMeshModifierSolidify(mesh, 0.1f);

        var again = LoadEditMeshFromMemory(GetEditMeshRecipe(mesh));
        _made.Add(again);
        GetEditMeshModifiers(again).Should().Equal("mirror xz at 0 0 -1", "array 2 0 3 0", "array curve 0 0 0 5 0 0", "bevel 0.05 2 45", "solidify 0.1");
        GetEditMeshModifiedInfo(again).Should().Be(GetEditMeshModifiedInfo(mesh));
    }

    [Fact]
    public void What_Is_Built_Is_The_Mesh_With_Its_Modifiers()
    {
        var mesh = Box(Vector3.One);
        EditMeshModifierArray(mesh, 3, new Vector3(2, 0, 0));
        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();

        EditMeshBuild.Slots(data).Single().Indices.Length.Should().Be(3 * 12 * 3);
    }

    [Fact]
    public void A_Stack_Of_A_Subdivision_A_Mirror_And_An_Array_Builds_Within_Its_Budget()
    {
        // 6,144 faces of four corners after the subdivision, twice that mirrored and four times
        // that repeated, 49,152 faces and 98,304 triangles built.
        var mesh = Box(new Vector3(1, 2, 2));
        EditMeshModifierSubdivision(mesh, 5);
        EditMeshModifierMirror(mesh, "x", new Vector3(-0.5f, 0, 0));
        EditMeshModifierArray(mesh, 4, new Vector3(3, 0, 0));
        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();

        var clock = Stopwatch.StartNew();
        var built = EditMeshBuild.Slots(data);
        clock.Stop();

        built.Single().Indices.Length.Should().Be(6144 * 2 * 4 * 6);
        clock.ElapsedMilliseconds.Should().BeLessThan(Budget, "a stack this size builds within {0} ms", Budget);
    }

    // Measured at 176 to 205 ms over five runs on four cores of a Linux container, the first
    // build of each test process, so the budget allows a runner seven times slower.
    private const long Budget = 1500;

    [Fact]
    public void A_Modifier_Out_Of_Range_Is_Refused()
    {
        var mesh = Box(Vector3.One);
        var refused = new Action[]
        {
            () => EditMeshModifierMirror(mesh, "w"),
            () => EditMeshModifierMirror(mesh, "xx"),
            () => EditMeshModifierArray(mesh, 0, Vector3.UnitX),
            () => EditMeshModifierArrayCurve(mesh, [Vector3.Zero]),
            () => EditMeshModifierBevel(mesh, 0),
            () => EditMeshModifierSubdivision(mesh, 0),
            () => EditMeshModifierSolidify(mesh, 0),
            () => EditMeshModifierMove(mesh, 0, 0),
            () => EditMeshModifierRemove(mesh, 0),
            () => EditMeshModifierApply(mesh, 0),
        };
        foreach (var call in refused) call.Should().Throw<ArgumentException>();
        GetEditMeshModifiers(mesh).Should().BeEmpty();
    }
}
