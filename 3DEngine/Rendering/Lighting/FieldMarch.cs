using System.Globalization;
using System.Numerics;
using System.Text;

namespace Engine;

/// <summary>
/// A march through the window's scene distance field as the passes make one, on the field read back
/// from the GPU, which <c>field.march</c> answers with, each step's cascade and distance and the
/// cascade and cell the march stopped in.
/// </summary>
/// <remarks>
/// Each step is the one <c>traceField</c> in <c>scenefield.slang</c> takes, from the finest cascade
/// holding the point a cell inside its sides, the cells' blend of the distance less half a cell where
/// the cell keeps a double-sided face's plane, a surface met under a tenth of a cell, and a step of
/// the distance or a quarter of a cell, whichever is longer, 64 at most. A march that leaves every
/// cascade or takes its 64 steps meets nothing, so the light a hit marches toward reaches it, as
/// <c>directLight</c> in <c>gi.slang</c> takes it. The march toward the sun from a surface starts a
/// cell and a half off it along the field's normal there and a cell along the way, as a probe's hit
/// marches toward the sun where the window's shadow map does not hold it.
/// </remarks>
internal sealed class FieldMarch
{
    private readonly float[] _distances, _sheets;
    private readonly Vector4[] _cascades;
    private readonly int _size;
    // The cells a shift brought into a cascade that wait for its build, read in the next cascade out.
    private readonly (int Cascade, SceneFieldPlan.CellBox Cells)? _unbuilt;

    private FieldMarch(float[] distances, float[] sheets, Vector4[] cascades, int size, (int, SceneFieldPlan.CellBox)? unbuilt) =>
        (_distances, _sheets, _cascades, _size, _unbuilt) = (distances, sheets, cascades, size, unbuilt);

    /// <summary>The march's steps, each its distance along the way, the cascade and the distance to the nearest surface there.</summary>
    internal readonly record struct Step(float T, int Cascade, float Distance);

    /// <summary>
    /// Where a march met a surface along its way, or -1 where it met none, what ended it and its
    /// steps.
    /// </summary>
    internal sealed record Marched(float Met, string Ended, IReadOnlyList<Step> Steps);

    /// <summary>The window's field as its last build left it, or null with why where none is built.</summary>
    internal static FieldMarch? Read(World world, out string why)
    {
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device
            || renderer.RenderWorld.TryGet<SceneFieldRenderer>() is not { Field: { } field, Plan: { } plan })
        {
            why = "the window draws no scene distance field, which SetSceneField or global illumination builds";
            return null;
        }
        // Where each cascade lies as the passes read it (SceneFieldInfo), a cascade not yet built
        // holding a cell of 0, which the passes pass over.
        var cascades = new Vector4[plan.Cascades];
        for (int c = 0; c < plan.Cascades; c++)
            if (plan.BuiltOrigin(c) is { } origin)
                cascades[c] = new Vector4(origin, plan.CellOf(c));
        why = "";
        return new FieldMarch(device.ReadSceneField(field), device.ReadSceneFieldSheets(field), cascades, SceneFieldPlan.Resolution, plan.Unbuilt);
    }

    /// <summary>
    /// The finest built cascade holding p at least margin cells inside its sides and margin cells off
    /// those a shift brought in that wait for its build, or -1 (fieldCascade).
    /// </summary>
    internal int CascadeAt(Vector3 p, float margin)
    {
        for (int c = 0; c < _cascades.Length; c++)
        {
            var oc = _cascades[c];
            if (oc.W <= 0) continue;
            var cell = (p - new Vector3(oc.X, oc.Y, oc.Z)) / oc.W;
            if (cell.X >= margin && cell.Y >= margin && cell.Z >= margin && cell.X <= _size - margin && cell.Y <= _size - margin && cell.Z <= _size - margin
                && !(_unbuilt is (var waiting, var cells) && waiting == c && cell.X >= cells.X0 - margin && cell.Y >= cells.Y0 - margin && cell.Z >= cells.Z0 - margin
                    && cell.X <= cells.X1 + 1 + margin && cell.Y <= cells.Y1 + 1 + margin && cell.Z <= cells.Z1 + 1 + margin))
                return c;
        }
        return -1;
    }

    /// <summary>The width of cascade c's cells.</summary>
    internal float CellSize(int c) => _cascades[c].W;

    /// <summary>The cell of cascade c that p lies in.</summary>
    internal (int X, int Y, int Z) CellIn(int c, Vector3 p)
    {
        var cell = (p - new Vector3(_cascades[c].X, _cascades[c].Y, _cascades[c].Z)) / _cascades[c].W;
        return ((int)MathF.Floor(cell.X), (int)MathF.Floor(cell.Y), (int)MathF.Floor(cell.Z));
    }

    /// <summary>
    /// The distance from p to the nearest surface in cascade c, the cells blended as the GPU's
    /// sampler blends them, less half a cell times the length of the blended plane (fieldDistanceIn).
    /// </summary>
    internal float DistanceIn(int c, Vector3 p)
    {
        var oc = _cascades[c];
        var cell = Vector3.Clamp((p - new Vector3(oc.X, oc.Y, oc.Z)) / oc.W, new Vector3(0.5f), new Vector3(_size - 0.5f)) - new Vector3(0.5f);
        var at = new Vector3(MathF.Floor(cell.X), MathF.Floor(cell.Y), MathF.Floor(cell.Z));
        var f = cell - at;
        float distance = 0;
        var plane = Vector3.Zero;
        for (int corner = 0; corner < 8; corner++)
        {
            var (ox, oy, oz) = (corner & 1, (corner >> 1) & 1, corner >> 2);
            int x = Math.Min((int)at.X + ox, _size - 1), y = Math.Min((int)at.Y + oy, _size - 1), z = Math.Min((int)at.Z + oz, _size - 1);
            var weight = (ox == 1 ? f.X : 1 - f.X) * (oy == 1 ? f.Y : 1 - f.Y) * (oz == 1 ? f.Z : 1 - f.Z);
            var index = ((c * _size + z) * _size + y) * _size + x;
            distance += _distances[index] * weight;
            plane += new Vector3(_sheets[index * 3], _sheets[index * 3 + 1], _sheets[index * 3 + 2]) * weight;
        }
        return distance - 0.5f * oc.W * Math.Clamp(plane.Length(), 0, 1);
    }

    /// <summary>The way out from the nearest surface at p, the distance's gradient over half a cell (fieldNormal).</summary>
    internal Vector3 Normal(Vector3 p)
    {
        var c = CascadeAt(p, 1.5f);
        if (c < 0) return Vector3.UnitY;
        var e = _cascades[c].W * 0.5f;
        var g = new Vector3(
            DistanceIn(c, p + new Vector3(e, 0, 0)) - DistanceIn(c, p - new Vector3(e, 0, 0)),
            DistanceIn(c, p + new Vector3(0, e, 0)) - DistanceIn(c, p - new Vector3(0, e, 0)),
            DistanceIn(c, p + new Vector3(0, 0, e)) - DistanceIn(c, p - new Vector3(0, 0, e)));
        return g.LengthSquared() > 1e-12f ? Vector3.Normalize(g) : Vector3.UnitY;
    }

    /// <summary>A march from origin along way between tMin and tMax (traceField).</summary>
    internal Marched March(Vector3 origin, Vector3 way, float tMin, float tMax)
    {
        var steps = new List<Step>();
        var t = tMin;
        for (int i = 0; i < 64 && t < tMax; i++)
        {
            var p = origin + way * t;
            var c = CascadeAt(p, 1);
            if (c < 0) return new Marched(-1, "left every cascade", steps);
            var cell = _cascades[c].W;
            var d = DistanceIn(c, p);
            steps.Add(new Step(t, c, d));
            if (d < cell * 0.1f) return new Marched(t, "met a surface", steps);
            t += MathF.Max(d, cell * 0.25f);
        }
        return new Marched(-1, t >= tMax ? "reached its end" : "took its 64 steps", steps);
    }

    /// <summary>
    /// The answer of <c>field.march</c>, a march from origin along way, and with toward a second
    /// from the surface the first met along toward, as a hit marches toward a light.
    /// </summary>
    internal string Describe(Vector3 origin, Vector3 way, Vector3? toward)
    {
        var text = new StringBuilder();
        var first = Leg(text, "from", origin, way, 0);
        if (toward is not { } next) return text.ToString();
        if (first < 0)
        {
            text.Append("and meeting no surface it has none to march on from");
            return text.ToString();
        }
        // As directLight starts a hit's march, by the cell of the finest cascade holding the hit.
        var hit = origin + way * first;
        var c = CascadeAt(hit, 1);
        var cell = c < 0 ? 0 : _cascades[c].W;
        Leg(text, "then from the surface, a cell and a half off it,", hit + Normal(hit) * (cell * 1.5f), next, cell);
        return text.ToString();
    }

    // One march's lines, where it met a surface, in which cascade and cell, and its steps.
    private float Leg(StringBuilder text, string start, Vector3 origin, Vector3 way, float tMin)
    {
        var marched = March(origin, way, tMin, 1e5f);
        text.Append(CultureInfo.InvariantCulture, $"{start} {Point(origin)} along {Point(way)}, ");
        if (marched.Met >= 0)
        {
            var p = origin + way * marched.Met;
            var c = CascadeAt(p, 1);
            var (x, y, z) = CellIn(c, p);
            text.Append(CultureInfo.InvariantCulture,
                $"met a surface {marched.Met:0.###} along, at {Point(p)}, in cascade {c}'s cell {x}, {y}, {z} of {_cascades[c].W:0.###}, its normal {Point(Normal(p))}, in {marched.Steps.Count} steps\n");
        }
        else
            text.Append(CultureInfo.InvariantCulture, $"{marched.Ended} after {marched.Steps.Count} steps, meeting no surface\n");
        foreach (var step in marched.Steps)
            text.Append(CultureInfo.InvariantCulture, $"  {step.T:0.###} cascade {step.Cascade} distance {step.Distance:0.###}\n");
        return marched.Met;
    }

    private static string Point(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}");
}
