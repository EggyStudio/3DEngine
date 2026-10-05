using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>A <see cref="ModelRef"/>'s path found from the asset folder, or as a <see cref="SceneRef"/>'s is.</summary>
[Trait("Category", "Unit")]
public sealed class ModelRefPathTests : IDisposable
{
    // Folders of their own beside the test assembly and in its source folder, as a program's
    // resources and its staged assets are.
    private readonly TestFolder _beside = TestFolder.At(Path.Combine(AppContext.BaseDirectory, "modelref-beside-" + Guid.NewGuid().ToString("N")[..8]));
    private readonly TestFolder _assets = TestFolder.At(Path.Combine(AppContext.BaseDirectory, "source", "modelref-assets-" + Guid.NewGuid().ToString("N")[..8]));

    public void Dispose()
    {
        _beside.Dispose();
        _assets.Dispose();
    }

    [Fact]
    public void A_Path_In_The_Asset_Folder_Is_Taken_As_It_Is()
    {
        File.WriteAllText(Path.Combine(_assets.Path, "box.obj"), "");
        var path = Path.GetFileName(_assets.Path) + "/box.obj";

        ModelRefSystem.AssetPath(path).Should().Be(path);
    }

    [Fact]
    public void A_Path_Beside_The_Program_Is_Found_As_A_SceneRef_Is()
    {
        File.WriteAllText(Path.Combine(_beside.Path, "house.obj"), "");
        var path = Path.GetFileName(_beside.Path) + "/house.obj";

        var asset = ModelRefSystem.AssetPath(path);

        asset.Should().Be(Path.Combine("..", Path.GetFileName(_beside.Path), "house.obj"), "the asset server reads from the source folder");
        File.Exists(Path.Combine(AppContext.BaseDirectory, "source", asset)).Should().BeTrue();
    }

    [Fact]
    public void A_Path_That_Names_No_File_Is_Left_For_The_Asset_Server_To_Report()
    {
        ModelRefSystem.AssetPath("nowhere/missing.obj").Should().Be("nowhere/missing.obj");
    }
}
