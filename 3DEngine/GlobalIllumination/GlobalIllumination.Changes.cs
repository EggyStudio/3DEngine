using System.Numerics;
using System.Runtime.InteropServices;
using static Engine.Illumination;

namespace Engine;

// Whether the window's lights or the field changed within a cycle of sixteen frames, which begins
// every screen probe's cycle of places again (gi_screen_filter.slang) and every carried world
// probe's (gi_merge.slang), and how far the world probes' cascades moved.
internal sealed partial class GlobalIlluminationRenderer
{
    // As many frames after a change as its light takes to settle, the light bounced off the walls
    // the frame before falling to a share of itself each frame, so a cycle gathered over them reads
    // part of the light before. A closed room built about an eye under the open sky, its first
    // whole cycle gathered from the build on, reads the open floor's light in its walls, which a
    // cycle gathered after these frames leaves out.
    private const int SettlingFrames = 16;

    // The frame before's lights, whether this frame's differ, and the last frame the lights or the
    // field changed.
    private float[]? _lightSignature;
    private bool[]? _lightPlaces;
    private bool _lightsChanged;
    private long _changedAt = long.MinValue / 2;

    // The switches global illumination was drawn with the frame before, and whether this frame's
    // differ, which begins every cycle again as a change of the lights does, so a part left out or
    // put back shows at once where the cycles would ease into it over two of theirs.
    private (bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, int, float, ScreenGather, GatherSize)? _switchesBefore;
    private bool _switchesChanged;

    // The boxes of the world whose surfaces or light changed this frame, each build of the field's
    // box of what came or went grown by its cell and each lamp's that changed grown by its reach,
    // and whether the sun, the sky or the gain changed, which changes the light on every surface
    // (SplitChanges).
    private readonly List<SceneFieldPlan.Box> _splitChanges = [];
    private bool _splitChangedWhole;

    // The least normal half float, which the field's cells keep their light in, below which a
    // lamp's light on a surface is not counted in its reach.
    private const float LeastKeptLight = 1f / 16384;

    // Whether the carried probes' rays moved the frame before, or null before the first frame.
    private bool? _movingBefore;

    // The share of a carried probe's own light this frame takes in the gather's mean, one over the
    // frames since the lights or the field last changed, sixteen at most (gi_ambient.slang).
    private float CarriedShare => 1f / Math.Min(16, _frame - _changedAt + 1);

    // The frames since the lights or the field last changed, 0 on the frame they did, which begins a
    // carried probe's cycle again (gi_merge.slang).
    private uint CarriedSince => (uint)Math.Min(_frame - _changedAt, 1000);

    // Where a carried probe's cycle of sixteen frames stands, counted from the last change, so the
    // first whole cycle is gathered in the sixteen frames after it.
    private uint CarriedPhase => (uint)((_frame - _changedAt) & 15);

    // Whether the lights or the field changed within the frames a change's light takes to settle.
    private bool Settling => _frame - _changedAt < SettlingFrames;

    // Notes the frame as one the lights, the field or the switches changed in, where any did. A
    // build of any cascade counts wherever it lies, since the last cascade's rays carry a change from
    // past the screen's probes' reach, as a roof five units over a floor seen through it.
    private void NoteChanged()
    {
        if (_lightsChanged || _switchesChanged || _fieldRebuilt != 0)
            _changedAt = _frame;
    }

    // Notes whether a part of global illumination was left out or put back since the frame before.
    private void NoteSwitches(GlobalIlluminationSettings s)
    {
        var now = (s.MergeOff, s.AgainOff, s.FollowOff, s.GlowOff, s.AlbedoOff, s.SpecularOff, s.NormalMapsOff, s.ThroughOff, s.HistoryOff,
            s.FilterOff, s.ScreenProbes, s.MeshesTraced, s.OwnFieldMarched, s.OwnPlaceOff, s.FootprintOff, s.Alone, s.BounceGain, s.Gather, s.GatherSize);
        _switchesChanged = _switchesBefore is { } before && before != now;
        _switchesBefore = now;
    }

    // Notes whether the sun, the sky or a light differs from the frame before's by more than a
    // hundredth, of its value or of a unit where that is smaller, and a light's place by more than
    // a hundredth of a unit, since a place far from the world's origin is large and a hundredth of
    // it spans rooms.
    private void NoteLights(byte[] lights)
    {
        var (signature, places) = LightSignature(lights);
        _lightsChanged = _lightSignature is not { } was || Differ(was, signature, places);
        _splitChanges.Clear();
        _splitChangedWhole = false;
        if (_lightsChanged && _lightSignature is { } before) NoteLightBoxes(before, signature, places);
        (_lightSignature, _lightPlaces) = (signature, places);
    }

    // Notes the boxes of the lights that differ from the frame before's by more than NoteLights
    // tells, each lamp that differs, came or went its place before and its place now each grown by
    // its reach, a lamp after one that came or went counted as changed, and the field's whole
    // extent where the sun, the sky or the gain differs. The glow lights give no box.
    private void NoteLightBoxes(float[] was, float[] now, bool[] places)
    {
        bool Apart(int i) => i >= was.Length || i >= now.Length || MathF.Abs(was[i] - now[i])
            > (places[i] ? 0.01f : 0.01f * MathF.Max(1, MathF.Max(MathF.Abs(was[i]), MathF.Abs(now[i]))));
        // The sun's way and light, the sky's and the count of lamps, then the gain.
        const int head = 14, count = 12;
        for (int i = 0; i < head; i++)
            if (i != count && Apart(i))
            {
                _splitChangedWhole = true;
                return;
            }
        var size = Marshal.SizeOf<LightUboEntry>() / 4;
        int before = Math.Clamp((int)was[count], 0, LightingUboPacker.MaxLights), after = Math.Clamp((int)now[count], 0, LightingUboPacker.MaxLights);
        for (int k = 0; k < Math.Max(before, after); k++)
        {
            var at = head + k * size;
            if (k < before && k < after && !Enumerable.Range(at, size).Any(Apart)) continue;
            if (k < before) _splitChanges.Add(LampReach(was, at));
            if (k < after) _splitChanges.Add(LampReach(now, at));
        }
    }

    // A lamp's place in the lights' signature at at grown along each axis by its reach, its range
    // where it has one, and else the distance past which its light on a surface, its brightest
    // color over the distance squared, falls below what the field's cells keep.
    private static SceneFieldPlan.Box LampReach(float[] signature, int at)
    {
        var place = new Vector3(signature[at], signature[at + 1], signature[at + 2]);
        var (range, brightest) = (signature[at + 7], MathF.Max(signature[at + 8], MathF.Max(signature[at + 9], signature[at + 10])));
        return new SceneFieldPlan.Box(place, place).Grown(range > 0 ? range : MathF.Sqrt(MathF.Max(brightest, 0) / LeastKeptLight));
    }

    // The frame's boxes of changes against the frame's origin, the coarsest cascade of the field
    // alone where the sun, the sky or the gain changed, which the split cascades' ways whose rays
    // may cross one let their light go by (GpuSplitStore.Changes).
    private List<SceneFieldPlan.Box> SplitChanges(RenderWorld renderWorld, RenderOrigin origin)
    {
        var boxes = new List<SceneFieldPlan.Box>();
        if (_splitChangedWhole && renderWorld.TryGet<SceneFieldRenderer>()?.Plan?.Extent is { } extent)
            boxes.Add(extent);
        else
            foreach (var box in _splitChanges)
                boxes.Add(box);
        for (int k = 0; k < boxes.Count; k++)
            boxes[k] = new SceneFieldPlan.Box(origin.Against(boxes[k].Min), origin.Against(boxes[k].Max));
        return boxes;
    }

    // The frame before's lights read in a world a program moved by a whole step, each place in them
    // moved, so the step is no change of the lights. The places come three to a place, x, y and z,
    // and lie in runs of whole places.
    private void MoveLightSignature(WorldStep step)
    {
        if (_lightSignature is not { } signature || _lightPlaces is not { } places) return;
        for (int i = 0, run = 0; i < signature.Length; i++)
        {
            if (!places[i])
            {
                run = i + 1;
                continue;
            }
            signature[i] = (float)(signature[i] + ((i - run) % 3) switch { 0 => step.X, 1 => step.Y, _ => step.Z });
        }
    }

    /// <summary>Whether the lights of <paramref name="after"/> differ from those of <paramref name="before"/> as a frame's are told to (NoteLights).</summary>
    internal static bool LightsDiffer(byte[] before, byte[] after)
    {
        var (was, _) = LightSignature(before);
        var (now, places) = LightSignature(after);
        return Differ(was, now, places);
    }

    private static bool Differ(float[] was, float[] now, bool[] places) =>
        was.Length != now.Length || !was.AsSpan().SequenceEqual(now) && Enumerable.Range(0, was.Length).Any(i => MathF.Abs(was[i] - now[i])
            > (places[i] ? 0.01f : 0.01f * MathF.Max(1, MathF.Max(MathF.Abs(was[i]), MathF.Abs(now[i])))));

    // The sun's way and light, the sky's, the count of lights and the gain, each point and spot
    // light, and the small emitters carried as lights with their box (GlowLights), from the lights'
    // buffer, leaving out the frame's number and the sun's map, which move with the frames and the
    // camera. Without the emitters, a panel turned dark keeps a carried probe's cycle of the light
    // it gave, the room it lit still reading its light.
    // Beside it, which of its values are places in the world, each lamp's and each emitter's and
    // the corners of the box the emitters reach.
    private static (float[] Signature, bool[] Places) LightSignature(byte[] lights)
    {
        var floats = MemoryMarshal.Cast<byte, float>(lights.AsSpan());
        var count = (int)floats[12];
        var size = Marshal.SizeOf<LightUboEntry>() / 4;
        var glows = GlowLights.Offset / 4;
        var lamps = Math.Min(count * size, floats.Length - 16);
        var glowFloats = Math.Min(GlowLights.Head / 4 + (int)floats[glows] * GlowLights.Bytes / 4, floats.Length - glows);
        float[] signature = [.. floats[..13], floats[14], .. floats.Slice(16, lamps), .. floats.Slice(glows, glowFloats)];
        var places = new bool[signature.Length];
        void Place(int at)
        {
            for (int k = at; k < at + 3 && k < places.Length; k++) places[k] = true;
        }
        // Each lamp's place is the first three of its values, after the head's fourteen.
        for (int at = 14; at < 14 + lamps; at += size) Place(at);
        // The emitters' box corners after their count, and each emitter's middle first among its values.
        var glow = 14 + lamps;
        if (glowFloats > 0)
        {
            Place(glow + 1);
            Place(glow + 4);
            for (int at = glow + GlowLights.Head / 4; at < glow + glowFloats; at += GlowLights.Bytes / 4) Place(at);
        }
        return (signature, places);
    }

    // How far each cascade's probes moved since the frame before, in probes, where it lay somewhere
    // before, which the merge reads a carried probe's cycle at its place then by.
    private (int X, int Y, int Z)?[] Shifts(Vector3?[] origins, SceneFieldPlan? plan, int cascades)
    {
        var shifts = new (int X, int Y, int Z)?[cascades];
        for (int c = 0; c < cascades; c++)
            if (origins[c] is { } now && c < _origins.Length && _origins[c] is { } before && plan is not null)
            {
                var moved = (now - before) / (plan.CellOf(plan.CascadeOfProbes(c, cascades)) * ProbeSpacing);
                shifts[c] = ((int)MathF.Round(moved.X), (int)MathF.Round(moved.Y), (int)MathF.Round(moved.Z));
            }
        return shifts;
    }
}
