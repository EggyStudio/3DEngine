using System.Runtime.InteropServices;

namespace Engine;

// Whether the window's lights or the field changed within a cycle of the screen probes' sixteen
// places, which begins every probe's cycle again (gi_screen_filter.slang).
internal sealed partial class GlobalIlluminationRenderer
{
    // As many frames after a change as its light takes to settle, the light bounced off the walls
    // the frame before falling to a share of itself each frame, so a cycle gathered over them reads
    // part of the light before. A closed room built about an eye under the open sky, its first
    // whole cycle gathered from the build on, read 0.0033 of the open floor's light in its walls
    // at High at the third frame of a heading, and 0.00007 with the cycle gathered after these.
    private const int SettlingFrames = 16;

    // The frame before's lights, whether this frame's differ, and the last frame the lights or the
    // field changed.
    private float[]? _lightSignature;
    private bool _lightsChanged;
    private long _changedAt = long.MinValue / 2;

    // Whether the lights or the field changed within the frames a change's light takes to settle.
    private bool Settling => _frame - _changedAt < SettlingFrames;

    // Notes the frame as one the lights or the field changed in, where either did. A build of any
    // cascade counts wherever it lies, since the last cascade's rays carry a change from past the
    // screen's probes' reach, as a roof five units over a floor seen through it.
    private void NoteChanged()
    {
        if (_lightsChanged || _fieldRebuilt != 0)
            _changedAt = _frame;
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

    // The sun's way and light, the sky's, the count of lights and the gain, and each point and spot
    // light, from the lights' buffer, leaving out the frame's number and the sun's map, which move
    // with the frames and the camera.
    private static float[] LightSignature(byte[] lights)
    {
        var floats = MemoryMarshal.Cast<byte, float>(lights.AsSpan());
        var count = (int)floats[12];
        var size = Marshal.SizeOf<LightUboEntry>() / 4;
        return [.. floats[..13], floats[14], .. floats.Slice(16, Math.Min(count * size, floats.Length - 16))];
    }
}
