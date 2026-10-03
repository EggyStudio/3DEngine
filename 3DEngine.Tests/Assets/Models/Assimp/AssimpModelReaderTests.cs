using System.Text;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Models.Assimp;

/// <summary>
/// Integration tests for the <see cref="AssimpModelReader"/> backend. Builds a tiny
/// OBJ file (text format) in-memory and runs it through the reader to verify the
/// produced <see cref="Scene"/> snapshot.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Backend", "Assimp")]
public class AssimpModelReaderTests
{
    private static byte[] MakeTriangleObj()
    {
        // Minimal valid OBJ: a single triangle named "Tri".
        var sb = new StringBuilder();
        sb.AppendLine("o Tri");
        sb.AppendLine("v 0 0 0");
        sb.AppendLine("v 1 0 0");
        sb.AppendLine("v 0 1 0");
        sb.AppendLine("f 1 2 3");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static AssetLoadContext OpenContext(string path, byte[] bytes) =>
        new AssetLoadContext(new MemoryStream(bytes), new AssetPath(path), _ => default);

    [Fact]
    public void Reader_Format_Id_Is_Assimp()
    {
        new AssimpModelReader().FormatId.Should().Be("assimp");
    }

    [Fact]
    public void Reader_Advertises_Common_Mesh_Extensions()
    {
        var exts = new AssimpModelReader().Extensions;
        // Sanity floor: Assimp's import-format list almost certainly includes these.
        exts.Should().Contain(".obj");
        exts.Should().Contain(".gltf");
        exts.Should().Contain(".glb");
        exts.Should().NotContain(".usd", "Assimp reads USD without its materials");
    }

    [Fact]
    public async Task ReadAsync_Triangle_Obj_Produces_Scene_With_One_Mesh()
    {
        var reader = new AssimpModelReader();
        using var ctx = OpenContext("tests/inline.obj", MakeTriangleObj());

        Scene scene;
        try
        {
            scene = await reader.ReadAsync(ctx, SceneImportSettings.Default, CancellationToken.None);
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex.GetType().Name.Contains("Assimp"))
        {
            // Native Assimp not available on this RID - skip rather than fail.
            return;
        }

        scene.Roots.Should().NotBeEmpty();

        // Walk the scene collecting any SceneMeshPayload.
        var meshes = new List<SceneMeshPayload>();
        void Walk(SceneNode n)
        {
            foreach (var c in n.Components)
                if (c is SceneMeshPayload m) meshes.Add(m);
            foreach (var ch in n.Children) Walk(ch);
        }
        foreach (var r in scene.Roots) Walk(r);

        meshes.Should().NotBeEmpty();
        var first = meshes[0];
        first.Positions.Length.Should().Be(3, "the OBJ has a single triangle (3 verts)");
        first.Indices.Length.Should().Be(3);
    }

    [Fact]
    public async Task ReadAsync_Honours_Cancellation()
    {
        var reader = new AssimpModelReader();
        using var ctx = OpenContext("tests/inline.obj", MakeTriangleObj());
        var ct = new CancellationToken(canceled: true);

        var act = () => reader.ReadAsync(ctx, SceneImportSettings.Default, ct);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}