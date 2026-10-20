using System.Numerics;

namespace Engine;

/// <summary>
/// The spatial processor the audio server works out each spatial voice's pan and volume through,
/// every frame, from where the voice and the listener are and which way each faces.
/// </summary>
/// <remarks>
/// The server starts with the engine's own, which pans a voice toward the side of the listener it
/// is on and makes it quieter with distance (see <see cref="AudioVoiceParams.NearDistance"/>). A
/// richer one, as one of head-related transfer functions or of occlusion, takes its place.
/// </remarks>
public interface ISpatialAudioProcessor : IDisposable
{
    /// <summary>Stable processor identifier (e.g. <c>"steamaudio"</c>).</summary>
    string ProcessorId { get; }

    /// <summary>Initializes the processor (allocates DSP context, loads HRTF).</summary>
    void Initialize();

    /// <summary>Computes per-voice spatial parameters for the current frame.</summary>
    SpatialResult Compute(int voiceId, Vector3 sourcePosition, Vector3 listenerPosition, in SpatialContext context);
}
