using System.Numerics;

namespace Engine;

/// <summary>
/// The spatial processor the audio server starts with, which pans a voice toward the side of the
/// listener it is on and makes it quieter the further it is, full within its near distance and
/// silent at its far one.
/// </summary>
/// <remarks>
/// <para>
/// The pan is the direction from the listener to the voice against the listener's across vector,
/// its forward crossed with its up, from -1 at its left to 1 at its right, and 0 for a voice ahead,
/// behind, or where the listener stands. The volume is 1 within the near distance and falls past it
/// as the inverse of the distance, reaching 0 at the far one,
/// <c>(near / d - near / far) / (1 - near / far)</c>, so a voice twice as far as another past
/// the near distance is about half as loud until the far distance draws near. A voice given an
/// orientation and a directivity is quieter away from the way it faces, by
/// <c>(1 - w) + w * max(0, cos)^p</c> of the angle from its forward to the listener.
/// </para>
/// <para>
/// A richer processor, as one of head-related transfer functions, takes its place through the
/// server's setter, and the backend plays the pan and the volume either gives.
/// </para>
/// </remarks>
internal sealed class DistanceSpatialProcessor : ISpatialAudioProcessor
{
    /// <inheritdoc />
    public string ProcessorId => "engine";

    /// <inheritdoc />
    public void Initialize() { }

    /// <inheritdoc />
    public SpatialResult Compute(int voiceId, Vector3 sourcePosition, Vector3 listenerPosition, in SpatialContext context)
    {
        var toward = sourcePosition - listenerPosition;
        var distance = toward.Length();
        var across = Vector3.Cross(context.ListenerForward, context.ListenerUp);
        var pan = distance < 1e-4f || across.LengthSquared() < 1e-8f
            ? 0f
            : Math.Clamp(Vector3.Dot(toward / distance, Vector3.Normalize(across)), -1f, 1f);

        var near = MathF.Max(context.NearDistance, 1e-3f);
        var far = MathF.Max(context.FarDistance, near * 1.001f);
        var gain = distance <= near ? 1f
            : distance >= far ? 0f
            : Math.Clamp((near / distance - near / far) / (1 - near / far), 0f, 1f);

        var directivity = 1f;
        if (context.DipoleWeight > 0 && distance > 1e-4f)
        {
            var facing = MathF.Max(0, Vector3.Dot(Vector3.Normalize(context.SourceForward), -toward / distance));
            directivity = 1 - context.DipoleWeight + context.DipoleWeight * MathF.Pow(facing, MathF.Max(context.DipolePower, 0));
        }

        return new SpatialResult
        {
            Pan = pan,
            DistanceAttenuation = gain,
            DirectivityAttenuation = directivity,
            VolumeAttenuation = gain * directivity,
        };
    }

    /// <inheritdoc />
    public void Dispose() { }
}
