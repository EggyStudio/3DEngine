using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>A cube texture made from an image of its six faces, in each of raylib's layouts.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class CubemapTests : IDisposable
{
    private readonly App _app = new App(Config.Default with { Headless = true, HeadlessFps = 240 }).AddPlugin(new DefaultPlugins());

    public CubemapTests() => UseApp(_app);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // A color for each face, +X, -X, +Y, -Y, +Z and -Z, so the order the cube is given them in shows.
    private static readonly Color[] FaceColors =
    [
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 255, 0), new(255, 0, 255), new(0, 255, 255),
    ];

    // An image of faces size texels wide with each face where the layout puts it, given as each
    // face's column and row in faces.
    private static Image Laid(int columns, int rows, (int Column, int Row)[] at, int size = 4)
    {
        var image = GenImageColor(columns * size, rows * size, Color.Black);
        for (int face = 0; face < 6; face++)
            ImageDrawRectangle(ref image, at[face].Column * size, at[face].Row * size, size, size, FaceColors[face]);
        return image;
    }

    // The faces the store was given for the cube, one under the next, by each face's middle texel.
    private Color[] FacesOf(Texture2D cube)
    {
        var upload = _app.World.Resource<TextureStore>().Take().Uploads.Single(u => u.Id == cube.Id);
        upload.Cube.Should().BeTrue();
        var faces = new Image(upload.Rgba!, cube.Width, cube.Width * 6);
        return [.. Enumerable.Range(0, 6).Select(face => GetImageColor(faces, cube.Width / 2, cube.Width * face + cube.Width / 2))];
    }

    [Fact]
    public void A_Cross_Four_Faces_By_Three_Is_Found_And_Its_Faces_Taken_In_Order()
    {
        var cube = LoadTextureCubemap(Laid(4, 3, [(2, 1), (0, 1), (1, 0), (1, 2), (1, 1), (3, 1)]), CubemapLayout.AutoDetect);

        (cube.Width, cube.Height).Should().Be((4, 4));
        FacesOf(cube).Should().Equal(FaceColors);
    }

    [Fact]
    public void A_Cross_Three_Faces_By_Four_Is_Found_And_Its_Faces_Taken_In_Order()
    {
        var cube = LoadTextureCubemap(Laid(3, 4, [(1, 1), (1, 3), (1, 0), (1, 2), (0, 1), (2, 1)]), CubemapLayout.AutoDetect);

        FacesOf(cube).Should().Equal(FaceColors);
    }

    [Fact]
    public void Lines_Across_And_Down_Are_Found_And_Their_Faces_Taken_In_Order()
    {
        var across = LoadTextureCubemap(Laid(6, 1, [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0)]), CubemapLayout.AutoDetect);
        var down = LoadTextureCubemap(Laid(1, 6, [(0, 0), (0, 1), (0, 2), (0, 3), (0, 4), (0, 5)]), CubemapLayout.AutoDetect);

        var uploads = _app.World.Resource<TextureStore>().Take().Uploads;
        foreach (var cube in new[] { across, down })
        {
            var faces = new Image(uploads.Single(u => u.Id == cube.Id).Rgba!, 4, 24);
            Enumerable.Range(0, 6).Select(face => GetImageColor(faces, 2, 4 * face + 2)).Should().Equal(FaceColors);
        }
    }

    [Fact]
    public void An_Image_Of_No_Layout_Gives_No_Texture_And_A_Layout_Named_Is_Read_As_Named()
    {
        LoadTextureCubemap(GenImageColor(10, 10, Color.Red), CubemapLayout.AutoDetect).IsValid.Should().BeFalse("a square image has no layout raylib finds");

        // A cross named as such is read as one whatever its shape says.
        var cube = LoadTextureCubemap(Laid(4, 3, [(2, 1), (0, 1), (1, 0), (1, 2), (1, 1), (3, 1)]), CubemapLayout.CrossFourByThree);
        FacesOf(cube).Should().Equal(FaceColors);
    }

    [Fact]
    public void A_Cube_Takes_No_New_Pixels_And_Reads_Back_As_Nothing()
    {
        var cube = LoadTextureCubemap(Laid(6, 1, [(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0)]), CubemapLayout.LineHorizontal);

        // Its pixels are six faces, so pixels of its size would make it one image.
        UpdateTexture(cube, GenImageColor(4, 4, Color.White)).Should().BeFalse();
        _app.World.Resource<TextureStore>().Take().Uploads.Where(u => u.Id == cube.Id).Should().ContainSingle().Which.Cube.Should().BeTrue();
        _app.World.Resource<TextureStore>().IsCube(cube.Id).Should().BeTrue();
        UnloadTexture(cube);
        IsTextureValid(cube).Should().BeFalse();
    }
}
