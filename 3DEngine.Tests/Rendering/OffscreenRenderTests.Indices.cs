using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Mesh_Of_Few_Vertices_Takes_Two_Byte_Indices_And_One_Of_More_Keeps_Four_And_Both_Draw()
    {
        // Two quads seen from above, the left a mesh of its four vertices, the right one whose two
        // triangles index the last four of 70,000 vertices, the rest far off, so its indices drawn
        // as two bytes each would take vertices off the screen and leave the right half black.
        Open(64, 32);
        var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) };
        ModelVertex[] Quad(Vector3 at) => [.. corners.Select(c => new ModelVertex { Position = at + c, Normal = Vector3.UnitY })];
        var small = UploadMesh(Quad(new Vector3(-1.5f, 0, 0)), [0, 2, 1, 0, 3, 2]);
        var many = Enumerable.Repeat(new ModelVertex { Position = new Vector3(0, -500, 0), Normal = Vector3.UnitY }, 70_000).ToArray();
        Quad(new Vector3(1.5f, 0, 0)).CopyTo(many, many.Length - 4);
        uint last = (uint)many.Length - 4;
        var large = UploadMesh(many, [last, last + 2, last + 1, last, last + 3, last + 2]);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 4, CameraProjection.Orthographic));
            DrawMesh(small, new ModelMaterial(Color.White), Matrix4x4.Identity);
            DrawMesh(large, new ModelMaterial(Color.White), Matrix4x4.Identity);
            EndMode3D();
        }, "indices");

        var meshes = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GpuMeshes>()!;
        meshes.Get(small.Id)!.IndexType.Should().Be(IndexType.UInt16, "four vertices fit two bytes an index");
        meshes.Get(small.Id)!.Indices.Description.Size.Should().BeLessThan(6 * 4UL, "the GPU holds the six indices in two bytes each, a buffer rounded to 16");
        meshes.Get(large.Id)!.IndexType.Should().Be(IndexType.UInt32, "70,000 vertices do not");
        var (left, right) = (GetImageColor(image, 16, 16), GetImageColor(image, 48, 16));
        (left.R + left.G + left.B).Should().BeGreaterThan(300, "the quad of four vertices is drawn");
        (right.R + right.G + right.B).Should().BeGreaterThan(300, "the quad indexing past 65,535 is drawn");
        UnloadMesh(small);
        UnloadMesh(large);
    }
}
