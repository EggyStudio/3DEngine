using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The grid image's lines at their pixels, the prototype materials' colors and the one texture they
/// share, a model built from an edit mesh drawn with its slots' prototypes, and the texture
/// coordinates in meters that carry the grid across faces.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class PrototypeTests : IDisposable
{
    private const float Close = 1e-4f;
    private static readonly Color Face = new(230, 230, 230, 255), Line = new(89, 89, 89, 255), Fine = new(173, 173, 173, 255);
    private readonly List<EditMesh> _made = [];

    public PrototypeTests()
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

    [Fact]
    public void A_Grid_Of_A_Hundred_Pixels_A_Meter_Has_Its_Lines_At_The_Meter_The_Half_And_The_Quarters()
    {
        var grid = GenImageGrid(100, Face, Line, Fine);
        (grid.Width, grid.Height).Should().Be((100, 100));

        // A meter's line a pixel on each edge, the half's line of the line's color and a quarter's
        // of the fine one a pixel wide each, the face between, and no mark on a cell too small to
        // letter.
        GetImageColor(grid, 0, 10).Should().Be(Line);
        GetImageColor(grid, 99, 10).Should().Be(Line);
        GetImageColor(grid, 10, 0).Should().Be(Line);
        GetImageColor(grid, 50, 10).Should().Be(Line);
        GetImageColor(grid, 10, 50).Should().Be(Line);
        foreach (var quarter in new[] { 25, 75 })
        {
            GetImageColor(grid, quarter, 10).Should().Be(Fine);
            GetImageColor(grid, 10, quarter).Should().Be(Fine);
            // Where a meter's line crosses a quarter's, the meter's is drawn.
            GetImageColor(grid, quarter, 0).Should().Be(Line);
        }
        GetImageColor(grid, 10, 10).Should().Be(Face);
        GetImageColor(grid, 3, 3).Should().Be(Face);
    }

    [Fact]
    public void The_Default_Grid_Widens_Its_Lines_With_It_And_Marks_Its_Size()
    {
        var grid = GenImageGrid(PrototypePixelsPerMeter);

        // Four pixels of a meter's line on each edge, eight across where cells meet, the half's line
        // four pixels wide from 510 and a quarter's two from 255.
        foreach (var x in new[] { 0, 3, 1020, 1023 }) GetImageColor(grid, x, 300).Should().Be(Line);
        GetImageColor(grid, 4, 300).Should().Be(Face);
        new[] { 509, 510, 513, 514 }.Select(x => GetImageColor(grid, x, 300)).Should().Equal(Face, Line, Line, Face);
        new[] { 254, 255, 256, 257 }.Select(x => GetImageColor(grid, x, 300)).Should().Equal(Face, Fine, Fine, Face);

        // The mark's squares eight pixels a side from 20 in, the 1's top square the third of its row
        // and the m's third row three squares of five.
        new[] { (20, 20), (36, 20), (43, 27), (44, 20) }.Select(p => GetImageColor(grid, p.Item1, p.Item2)).Should().Equal(Face, Line, Line, Face);
        new[] { (68, 36), (76, 36), (84, 36), (92, 36) }.Select(p => GetImageColor(grid, p.Item1, p.Item2)).Should().Equal(Line, Line, Face, Line);
        GetImageColor(grid, 68, 20).Should().Be(Face);

        var small = () => GenImageGrid(15);
        small.Should().Throw<ArgumentOutOfRangeException>();
        var large = () => GenImageGrid(4097);
        large.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Each_Part_Of_A_Level_Has_A_Color_Of_Its_Own_And_Any_Other_Name_The_Grids_Gray()
    {
        var names = new[] { "floor", "wall", "ceiling", "stairs", "door", "window", "hazard" };
        names.Select(GetPrototypeColor).Distinct().Should().HaveCount(7).And.NotContain(Color.White);
        GetPrototypeColor("default").Should().Be(Color.White);
        GetPrototypeColor("Wall").Should().Be(Color.White, "the names are the slots' names, in lower case");
    }

    [Fact]
    public void Prototype_Materials_Share_One_Grid_Texture_Rough_And_Not_Metal()
    {
        var textures = GetApp().World.Resource<TextureStore>();
        var before = textures.Count;
        var floor = LoadMaterialPrototype("floor");
        var tinted = LoadMaterialPrototype(new Color(10, 20, 30, 255));

        textures.Count.Should().Be(before + 1, "every prototype material shows the one grid");
        floor.Texture.Should().Be(tinted.Texture);
        (floor.Texture.Width, floor.Texture.Mipmaps).Should().Be((PrototypePixelsPerMeter, 11));
        floor.Color.Should().Be(GetPrototypeColor("floor"));
        (floor.Roughness, floor.Metallic).Should().Be((0.85f, 0));
    }

    [Fact]
    public void A_Model_Built_From_An_Edit_Mesh_Draws_Each_Slot_With_Its_Prototype()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, new Vector3(6, 3, 6));
        EditMeshMaterial(mesh, "wall");
        EditMeshSelectFacing(mesh, -Vector3.UnitY);
        EditMeshMaterial(mesh, "floor");
        var model = LoadModelFromEditMesh(mesh);

        var colors = Enumerable.Range(0, model.Meshes.Length).Select(slot => model.Materials[model.MeshMaterial[slot]].Color).ToArray();
        colors.Should().Equal(Color.White, GetPrototypeColor("wall"), GetPrototypeColor("floor"));
        model.Materials.Should().OnlyContain(material => material.Texture.Id == model.Materials[0].Texture.Id && material.Texture.IsValid);

        // A slot added after takes its own prototype.
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "ceiling");
        UpdateModelFromEditMesh(model, mesh);
        model.Materials[model.MeshMaterial[3]].Color.Should().Be(GetPrototypeColor("ceiling"));
        foreach (var built in model.Meshes) UnloadMesh(built);
    }

    [Fact]
    public void Texture_Coordinates_Are_Meters_That_Carry_On_Across_Faces()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshSubdivide(mesh);
        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();

        // Each corner of the top's four faces takes its place along X and Z, whichever face it is
        // a corner of, so the grid runs on across them, and the top two meters wide spans two.
        var top = EditMeshBuild.Slots(data)[0].Vertices.Where(vertex => vertex.Normal == Vector3.UnitY).ToArray();
        top.Should().HaveCount(16);
        top.Should().OnlyContain(vertex => vertex.Uv == new Vector2(vertex.Position.X, vertex.Position.Z));
        (top.Max(vertex => vertex.Uv.X) - top.Min(vertex => vertex.Uv.X)).Should().BeApproximately(2, Close);
    }
}
