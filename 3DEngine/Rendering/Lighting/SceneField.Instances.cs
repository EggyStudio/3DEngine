using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

// The meshes of a build as the field's splat reads them.
internal sealed partial class SceneFieldRenderer
{
    // A build's instances as field_splat.slang's FieldInstance reads them, 96 bytes each, each
    // one's rows taking a place of its mesh to one against the cascade's corner, worked in doubles,
    // which the splat is handed as a corner at nothing, so a mesh far out and turned is put in the
    // cells with no float of the world's size in it, whether one has an inside, the share of light it
    // lets through, its roughness and its metallic share in its third count, with how many there
    // are, how many triangles they have in all, and whether one is emissive and thinner than a cell
    // of the build, which field_splat.slang lends the light of.
    private (IBuffer Buffer, int Count, int Triangles, bool ThinGlow) Instances(GraphicsDevice device, SceneFieldPlan.Instance[] instances, float cell,
        Vector3 corner)
    {
        var (thinGlow, against) = (false, new RenderOrigin(corner.X, corner.Y, corner.Z));
        var buffer = device.CreateBuffer(new BufferDesc((ulong)Math.Max(1, instances.Length) * 96, BufferUsage.Storage, CpuAccessMode.Write));
        var mapped = device.Map(buffer).Cast<uint>();
        int written = 0, triangles = 0;
        foreach (var instance in instances)
        {
            if (!_pooled.TryGetValue(instance.Vertices, out var pooled) || pooled.Count == 0) continue;
            var record = mapped.Slice(written * 24, 24);
            var (w, c, e) = (against.Against(instance.World), instance.Color, instance.Emission);
            ReadOnlySpan<float> rows = [w.M11, w.M21, w.M31, w.M41, w.M12, w.M22, w.M32, w.M42, w.M13, w.M23, w.M33, w.M43];
            record.CopyFrom(MemoryMarshal.Cast<float, uint>(rows));
            (record[12], record[13], record[14], record[15]) = ((uint)pooled.First, (uint)pooled.Count,
                (instance.DoubleSided ? 1u : 0u) | Tie(instance, pooled.Count) << 1 | Byte(instance.LightThrough) << 8 | Byte(instance.Roughness) << 16
                | Byte(instance.Metallic) << 24, (uint)triangles);
            var thinnest = Thinnest(instance);
            // An emitter global illumination carries as a glow light marks its cells and lends
            // none.
            var carried = Carried(instance, _plan!.CellSize);
            thinGlow |= !carried && thinnest > 0 && thinnest < cell;
            ReadOnlySpan<float> colors = [c.X, c.Y, c.Z, carried ? 2 : 1, e.X, e.Y, e.Z, thinnest];
            record.Slice(16).CopyFrom(MemoryMarshal.Cast<float, uint>(colors));
            triangles += pooled.Count;
            written++;
        }
        device.Unmap(buffer);
        return (buffer, written, triangles, thinGlow);
    }

    /// <summary>
    /// Seven bits of a mesh's own, from its matrix's turn and scale, its color and how many
    /// triangles it has, which end each word field_splat.slang keeps of a cell, so of two meshes'
    /// faces as near a cell and looking the same way the same one paints it in every build, every
    /// run and after the world moved.
    /// </summary>
    internal static uint Tie(in SceneFieldPlan.Instance instance, int triangles)
    {
        var w = instance.World;
        ReadOnlySpan<float> parts = [w.M11, w.M12, w.M13, w.M21, w.M22, w.M23, w.M31, w.M32, w.M33, instance.Color.X, instance.Color.Y, instance.Color.Z];
        var hash = 2166136261u ^ (uint)triangles;
        foreach (var part in parts) hash = (hash ^ BitConverter.SingleToUInt32Bits(part)) * 16777619u;
        return (hash ^ hash >> 7 ^ hash >> 14 ^ hash >> 21 ^ hash >> 28) & 127u;
    }

    // A share from 0 to 1 in 255ths.
    private static uint Byte(float share) => (uint)MathF.Round(Math.Clamp(share, 0, 1) * 255);

    // How thin an emissive instance is, its mesh's bounds along each of its own axes
    // scaled by its world matrix, the least of them, which field_splat.slang lends the light of
    // where it is under a cell, or 0 for one that emits nothing.
    private float Thinnest(SceneFieldPlan.Instance instance)
    {
        if (instance.Emission is { X: <= 0, Y: <= 0, Z: <= 0 }) return 0;
        if (!_bounds.TryGetValue(instance.Vertices, out var box)) _bounds[instance.Vertices] = box = SceneFieldPlan.Box.Of(instance.Vertices);
        var w = instance.World;
        var size = box.Max - box.Min;
        var x = size.X * new Vector3(w.M11, w.M12, w.M13).Length();
        var y = size.Y * new Vector3(w.M21, w.M22, w.M23).Length();
        var z = size.Z * new Vector3(w.M31, w.M32, w.M33).Length();
        return Math.Max(Math.Min(x, Math.Min(y, z)), 1e-4f);
    }
}
