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
    public void A_Grid_Of_A_Hundred_Pixels_A_Meter_Has_Its_Lines_At_The_Meter_And_Every_Tenth()
    {
        var grid = GenImageGrid(100, Face, Line, Fine);
        (grid.Width, grid.Height).Should().Be((100, 100));

        // A meter's line a pixel on each edge, a tenth's line a pixel at each tenth, and the face between.
        GetImageColor(grid, 0, 50).Should().Be(Line);
        GetImageColor(grid, 99, 50).Should().Be(Line);
        GetImageColor(grid, 50, 0).Should().Be(Line);
        foreach (var tenth in Enumerable.Range(1, 9)) GetImageColor(grid, tenth * 10, 55).Should().Be(Fine);
        GetImageColor(grid, 5, 55).Should().Be(Face);
        GetImageColor(grid, 15, 55).Should().Be(Face);

        // Where a meter's line crosses a tenth's, the meter's is drawn.
        GetImageColor(grid, 10, 0).Should().Be(Line);
    }

    [Fact]
    public void The_Default_Grid_Widens_Its_Lines_With_It()
    {
        var grid = GenImageGrid(PrototypePixelsPerMeter);

        // Four pixels of a meter's line on each edge, eight across where cells meet, and two of each
        // tenth's, from 101.4 rounded.
        foreach (var x in new[] { 0, 3, 1020, 1023 }) GetImageColor(grid, x, 500).Should().Be(Line);
        GetImageColor(grid, 4, 500).Should().Be(Face);
        GetImageColor(grid, 101, 500).Should().Be(Fine);
        GetImageColor(grid, 102, 500).Should().Be(Fine);
        GetImageColor(grid, 103, 500).Should().Be(Face);

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
