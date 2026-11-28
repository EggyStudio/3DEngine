using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// Where the field's cascades lie against the camera, about the eye or ahead along its look, and
// the part of the window the field's view draws for each.
public sealed partial class GlobalIlluminationTests
{
    // A floor 40 across and a unit thick with its top at height 0.
    private static void Floor(Model slab) =>
        DrawModelEx(slab, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(40, 1, 40), Color.White);

    private static SceneFieldPlan FieldPlan() =>
        GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!;

    [NeedsVulkanFact(SplitAtWindowSize = true)]
    [Trait("Category", "Render")]
    public void Each_Cascade_Lies_About_The_Eye_Or_Ahead_Along_The_Look_At_Two_Places_And_Two_Turns()
    {
        // A cascade's middle stands on a grid of 8 of its cells, at the step nearest the point it
        // lies about, so within 4 cells of that point along each axis. The point is the eye with
        // no share ahead, and the eye plus that share of the finest cascade's width along the look
        // with one, wherever the camera stands and whichever way it turns.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        foreach (var ahead in new[] { 0f, 0.75f })
        {
            SetSceneFieldAhead(ahead);
            foreach (var eye in new[] { new Vector3(0, 1.5f, 4.5f), new Vector3(7, 2.5f, -3) })
                foreach (var degrees in new[] { 0f, 135f })
                {
                    var look = Vector3.Transform(new Vector3(0, -0.3f, -1), Matrix4x4.CreateRotationY(degrees * MathF.PI / 180));
                    Capture(() => Floor(slab), new Camera3D(eye, eye + look, Vector3.UnitY, 50));
                    var plan = FieldPlan();
                    var point = eye + Vector3.Normalize(look) * (ahead * SceneFieldPlan.Resolution * plan.CellOf(0));
                    for (int c = 0; c < plan.Counted; c++)
                    {
                        var cell = plan.CellOf(c);
                        var corner = plan.BuiltOrigin(c);
                        corner.Should().NotBeNull($"cascade {c} is built");
                        var middle = corner!.Value + new Vector3(SceneFieldPlan.Resolution / 2 * cell);
                        var off = Vector3.Abs(middle - point) / cell;
                        MathF.Max(off.X, MathF.Max(off.Y, off.Z)).Should().BeLessThanOrEqualTo(SceneFieldPlan.SnapCells / 2 + 0.01f,
                            $"cascade {c}'s middle {middle} stands within half the snap of {point}, {ahead} ahead, the eye at {eye} turned {degrees} degrees");
                    }
                }
        }
        UnloadModel(slab);
    }

    [NeedsVulkanFact(SplitAtWindowSize = true)]
    [Trait("Category", "Render")]
    public void The_Field_View_Draws_Each_Cascade_Over_The_Floor_Where_Its_Box_Reaches_And_Nowhere_Else()
    {
        // Each pixel's ray meets the floor at a point, which the view of a cascade draws over where
        // the point lies in the cascade's box, half a cell in from its sides as the view marches
        // it, and leaves as the frame drew it outside. Points within three cells of a side, or past
        // 12 units from the eye where a pixel spans more than a cell, are not read.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var (eye, target) = (new Vector3(3.5f, 2, 0.5f), new Vector3(4.5f, 1.65f, 0.2f));
        var camera = new Camera3D(eye, target, Vector3.UnitY, 50);
        var settings = GetApp().World.Resource<SceneFieldSettings>();
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);
        var tan = MathF.Tan(50 * MathF.PI / 360);
        for (int c = 0; c < 2; c++)
        {
            settings.Shown = -1;
            var plain = Capture(() => Floor(slab), camera);
            settings.Shown = c;
            var shown = Capture(() => Floor(slab), camera);
            var plan = FieldPlan();
            var cell = plan.CellOf(c);
            var low = plan.BuiltOrigin(c)!.Value + new Vector3(cell / 2);
            var high = low + new Vector3((SceneFieldPlan.Resolution - 1) * cell);
            var (w, h) = (shown.Width, shown.Height);
            int inside = 0, outside = 0, wrong = 0;
            var said = new List<string>();
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    var way = forward + right * (((px + 0.5f) / w * 2 - 1) * tan * w / h) + up * ((1 - (py + 0.5f) / h * 2) * tan);
                    if (way.Y >= 0) continue;
                    var floor = eye + way * (-eye.Y / way.Y);
                    if (Vector3.Distance(floor, eye) > 12) continue;
                    var margin = MathF.Min(MathF.Min(floor.X - low.X, high.X - floor.X), MathF.Min(floor.Z - low.Z, high.Z - floor.Z));
                    if (MathF.Abs(margin) < 3 * cell) continue;
                    var (a, b) = (GetImageColor(plain, px, py), GetImageColor(shown, px, py));
                    var drawn = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 60;
                    if (margin > 0) inside++;
                    else outside++;
                    if (drawn == margin > 0) continue;
                    wrong++;
                    if (said.Count < 5) said.Add($"({px}, {py}) over {floor} {(drawn ? "drawn" : "left")}");
                }
            output.WriteLine($"cascade {c}: box {low} to {high}, {inside} pixels inside and {outside} outside read, {wrong} wrong");
            inside.Should().BeGreaterThan(100, $"cascade {c}'s box holds floor the camera sees");
            outside.Should().BeGreaterThan(20, $"floor past cascade {c}'s box lies within reach");
            wrong.Should().Be(0, $"the view draws cascade {c} over its box alone: {string.Join(", ", said)}");
        }
        settings.Shown = -1;
        UnloadModel(slab);
    }
}
