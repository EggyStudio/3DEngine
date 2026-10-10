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

    // Grows a block to hold a mesh's box placed by a world matrix: the box's middle placed, and
    // each half width the sum of the box's half widths along the matrix's rows (Arvo), so a mesh
    // moved without turning keeps a box as tight as its own.
    private static void Enclose(ref Block block, in Matrix4x4 world, Vector3 min, Vector3 max)
    {
        var middle = Vector3.Transform((min + max) / 2, world);
        var half = (max - min) / 2;
        var reach = new Vector3(
            MathF.Abs(world.M11) * half.X + MathF.Abs(world.M21) * half.Y + MathF.Abs(world.M31) * half.Z,
            MathF.Abs(world.M12) * half.X + MathF.Abs(world.M22) * half.Y + MathF.Abs(world.M32) * half.Z,
            MathF.Abs(world.M13) * half.X + MathF.Abs(world.M23) * half.Y + MathF.Abs(world.M33) * half.Z);
        block.Min = Vector3.Min(block.Min, middle - reach);
        block.Max = Vector3.Max(block.Max, middle + reach);
    }

    // The four side planes of a view, as (normal, distance) with the inside where the sum is not
    // negative, from a view-projection that takes row vectors to clip space (Gribb and Hartmann).
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

    /// <summary>
    /// Whether a view through <paramref name="viewProjection"/> may see a box, by its four side
    /// planes, and by its near and far planes too for a light's pass (<paramref name="light"/>).
    /// </summary>
    internal static bool Seen(in Matrix4x4 viewProjection, Vector3 min, Vector3 max, bool light = false) => new Frustum(viewProjection, light).Sees(min, max);

    // The planes a pass leaves blocks out by, made once for each view-projection it draws through.
    // A camera's pass uses the four sides alone, so its depth convention never leaves out what it
    // draws. A light's pass (depth) also leaves out a block before its near plane or past its far
    // one, Vulkan keeping depths from 0 to w, the shadow shader writing the position as the
    // light's matrix gives it and no pipeline clamping depth, so such a block is clipped whole
    // whether drawn or not; a cascade's box, which reaches far toward the light, holds every caster
    // that can shadow it.
    private readonly struct Frustum
    {
        private readonly Vector4 _left, _right, _bottom, _top, _near, _far;
        private readonly bool _depth;

        public Frustum(in Matrix4x4 viewProjection, bool depth)
        {
            (_left, _right, _bottom, _top) = Planes(viewProjection);
            _near = new Vector4(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
            _far = new Vector4(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44) - _near;
            _depth = depth;
        }

        public bool Sees(Vector3 min, Vector3 max) =>
            !(Outside(_left, min, max) || Outside(_right, min, max) || Outside(_bottom, min, max) || Outside(_top, min, max)
              || _depth && (Outside(_near, min, max) || Outside(_far, min, max)));

        // Whether the pass draws any of a batch, a batch with no blocks always, so a batch it draws
        // none of is passed over before its buffers are bound.
        public bool SeesAny(in Batch batch, Block[] blocks)
        {
            if (batch.BlockCount == 0) return true;
            for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
                if (Sees(blocks[i].Min, blocks[i].Max)) return true;
            return false;
        }
    }

    // Draws a batch, the blocks a view sees as runs of calls and a batch with none as one, and
    // answers how many calls it made.
    private static int DrawSeen(TrackedRenderPass pass, in Batch batch, Block[] blocks, in Frustum frustum)
    {
        if (batch.BlockCount == 0)
        {
            pass.DrawIndexed(batch.Mesh.IndexCount, batch.Count, 0, 0, batch.First);
            return 1;
        }

        int calls = 0;
        uint runFirst = 0, runCount = 0;
        for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
        {
            ref var block = ref blocks[i];
            var seen = frustum.Sees(block.Min, block.Max);
            block.Drawn |= seen;
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
}
