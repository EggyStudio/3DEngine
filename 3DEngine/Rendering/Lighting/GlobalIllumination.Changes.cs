using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

// Whether the window's lights or the field changed within a cycle of sixteen frames, which begins
// every screen probe's cycle of places again (gi_screen_filter.slang) and every carried world
// probe's (gi_merge.slang), and how far the world probes' cascades moved.
internal sealed partial class GlobalIlluminationRenderer
{
    // As many frames after a change as its light takes to settle, the light bounced off the walls
    // the frame before falling to a share of itself each frame, so a cycle gathered over them reads
    // part of the light before. A closed room built about an eye under the open sky, its first
    // whole cycle gathered from the build on, read the open floor's light in its walls, which a
    // cycle gathered after these frames leaves out.
    private const int SettlingFrames = 16;

    // The frame before's lights, whether this frame's differ, and the last frame the lights or the
    // field changed.
    private float[]? _lightSignature;
    private bool _lightsChanged;
    private long _changedAt = long.MinValue / 2;

    // The switches global illumination was drawn with the frame before, and whether this frame's
    // differ, which begins every cycle again as a change of the lights does, so a part left out or
    // put back shows at once where the cycles would ease into it over two of theirs.
    private (bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, int, float)? _switchesBefore;
    private bool _switchesChanged;

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
            s.FilterOff, s.ScreenOff, s.MeshesTraced, s.Alone, s.BounceGain);
        _switchesChanged = _switchesBefore is { } before && before != now;
        _switchesBefore = now;
    }

    // Notes whether the sun, the sky or a light differs from the frame before's by more than a
    // hundredth, of its value or of a unit where that is smaller.
    private void NoteLights(byte[] lights)
    {
        var signature = LightSignature(lights);
        _lightsChanged = _lightSignature is not { } was || was.Length != signature.Length
            || !was.AsSpan().SequenceEqual(signature) && Enumerable.Range(0, was.Length).Any(i => MathF.Abs(was[i] - signature[i]) > 0.01f * MathF.Max(1, MathF.Max(MathF.Abs(was[i]), MathF.Abs(signature[i]))));
        _lightSignature = signature;
    }

    // The sun's way and light, the sky's, the count of lights and the gain, each point and spot
    // light, and the small emitters carried as lights with their box (GlowLights), from the lights'
    // buffer, leaving out the frame's number and the sun's map, which move with the frames and the
    // camera. Without the emitters, a panel turned dark kept a carried probe's cycle of the light
    // it gave, the room it lit still reading its light.
    private static float[] LightSignature(byte[] lights)
    {
        var floats = MemoryMarshal.Cast<byte, float>(lights.AsSpan());
        var count = (int)floats[12];
        var size = Marshal.SizeOf<LightUboEntry>() / 4;
        var glows = GlowLights.Offset / 4;
        var glowFloats = GlowLights.Head / 4 + (int)floats[glows] * GlowLights.Bytes / 4;
        return [.. floats[..13], floats[14], .. floats.Slice(16, Math.Min(count * size, floats.Length - 16)), .. floats.Slice(glows, Math.Min(glowFloats, floats.Length - glows))];
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
