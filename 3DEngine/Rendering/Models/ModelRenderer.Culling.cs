using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    // The boxes of a segment's blocks, each around the spheres its instances' meshes fill: the
    // mesh's sphere moved by the world matrix, its radius grown by the matrix's largest scale.
    private static void Bound(Instance[] items, int count, int at, (Vector3 Center, float Radius) sphere, Block[] blocks, int firstBlock)
    {
        for (int start = 0, b = firstBlock; start < count; start += BlockSize, b++)
        {
            var end = Math.Min(count, start + BlockSize);
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            if (float.IsPositiveInfinity(sphere.Radius))
                (min, max) = (new Vector3(float.NegativeInfinity), new Vector3(float.PositiveInfinity));
            else
                for (int i = start; i < end; i++)
                {
                    ref readonly var instance = ref items[i];
                    var c = new Vector4(sphere.Center, 1);
                    var center = new Vector3(Vector4.Dot(instance.WorldX, c), Vector4.Dot(instance.WorldY, c), Vector4.Dot(instance.WorldZ, c));
                    // The length of each of the mesh's axes once placed, the largest of which grows the sphere.
                    var x = new Vector3(instance.WorldX.X, instance.WorldY.X, instance.WorldZ.X).LengthSquared();
                    var y = new Vector3(instance.WorldX.Y, instance.WorldY.Y, instance.WorldZ.Y).LengthSquared();
                    var z = new Vector3(instance.WorldX.Z, instance.WorldY.Z, instance.WorldZ.Z).LengthSquared();
                    var radius = new Vector3(sphere.Radius * MathF.Sqrt(MathF.Max(x, MathF.Max(y, z))));
                    min = Vector3.Min(min, center - radius);
                    max = Vector3.Max(max, center + radius);
                }
            blocks[b] = new Block { First = (uint)(at + start), Count = (uint)(end - start), Min = min, Max = max };
        }
    }

    // The four side planes of a view, as (normal, distance) with the inside where the sum is not
    // negative, from a view-projection that takes row vectors to clip space (Gribb and Hartmann).
    // Near and far are left out, so a depth convention, or a shadow box's reach toward the light,
    // never leaves out what a view draws.
    internal static (Vector4 Left, Vector4 Right, Vector4 Bottom, Vector4 Top) Planes(in Matrix4x4 m)
    {
        var x = new Vector4(m.M11, m.M21, m.M31, m.M41);
        var y = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var w = new Vector4(m.M14, m.M24, m.M34, m.M44);
        return (w + x, w - x, w + y, w - y);
    }

    internal static bool Outside(Vector4 plane, Vector3 min, Vector3 max)
    {
        // The corner of the box farthest along the plane's normal.
        var far = new Vector3(plane.X >= 0 ? max.X : min.X, plane.Y >= 0 ? max.Y : min.Y, plane.Z >= 0 ? max.Z : min.Z);
        return plane.X * far.X + plane.Y * far.Y + plane.Z * far.Z + plane.W < 0;
    }

    /// <summary>Whether a view through <paramref name="viewProjection"/> may see a box, by its four side planes.</summary>
    internal static bool Seen(in Matrix4x4 viewProjection, Vector3 min, Vector3 max)
    {
        var (left, right, bottom, top) = Planes(viewProjection);
        return !(Outside(left, min, max) || Outside(right, min, max) || Outside(bottom, min, max) || Outside(top, min, max));
    }

    // Draws a batch, a group's blocks a view sees as runs of calls and anything else as one, and
    // answers how many calls it made.
    private static int DrawSeen(TrackedRenderPass pass, in Batch batch, Block[] blocks, in Matrix4x4 viewProjection)
    {
        if (batch.BlockCount == 0)
        {
            pass.DrawIndexed(batch.Mesh.IndexCount, batch.Count, 0, 0, batch.First);
            return 1;
        }

        var (left, right, bottom, top) = Planes(viewProjection);
        int calls = 0;
        uint runFirst = 0, runCount = 0;
        for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
        {
            ref readonly var block = ref blocks[i];
            var seen = !(Outside(left, block.Min, block.Max) || Outside(right, block.Min, block.Max)
                || Outside(bottom, block.Min, block.Max) || Outside(top, block.Min, block.Max));
            if (seen && runCount > 0 && runFirst + runCount == block.First)
            {
                runCount += block.Count;
                continue;
            }
            if (runCount > 0)
            {
                pass.DrawIndexed(batch.Mesh.IndexCount, runCount, 0, 0, runFirst);
                calls++;
            }
            (runFirst, runCount) = seen ? (block.First, block.Count) : (0u, 0u);
        }
        if (runCount > 0)
        {
            pass.DrawIndexed(batch.Mesh.IndexCount, runCount, 0, 0, runFirst);
            calls++;
        }
        return calls;
    }

    // The ring of instances, with room for a region per frame slot of at least this call's
    // instances past those the frame has written already. A ring outgrown is replaced by one twice
    // the size, the old one kept until no frame in flight reads it, and the frame's earlier
    // commands keep the old one bound.
    private void EnsureInstanceRoom(IGraphicsDevice gfx, int instances)
    {
        if (_instanceRing is not null && _ringCursor + instances <= _ringCapacity) return;

        if (_instanceRing is not null) _retiredBuffers.Add((_frames, _instanceRing));
        _ringCapacity = Math.Max(Math.Max(1024, _ringCapacity * 2), _ringCursor + instances);
        _instanceRing = gfx.CreateBuffer(new BufferDesc((ulong)(SetRingFrames * _ringCapacity * Instance.Size), BufferUsage.Vertex, CpuAccessMode.Write));
        (gfx as GraphicsDevice)?.Name(_instanceRing, "Model instances");
    }
}
