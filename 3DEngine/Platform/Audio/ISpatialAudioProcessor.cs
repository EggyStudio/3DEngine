using System.Numerics;

namespace Engine;

/// <summary>
/// Optional spatial post-processor that augments raw <see cref="IAudioBackend"/> output
/// with engine-quality 3D parameters (distance attenuation, occlusion, HRTF direction).
/// None is built into the engine, and without one the backend's own 3D math is used.
/// </summary>
public interface ISpatialAudioProcessor : IDisposable
{
    /// <summary>Stable processor identifier (e.g. <c>"steamaudio"</c>).</summary>
    string ProcessorId { get; }

    /// <summary>Initializes the processor (allocates DSP context, loads HRTF).</summary>
    void Initialize();

    /// <summary>Computes per-voice spatial parameters for the current frame.</summary>
    SpatialResult Compute(int voiceId, Vector3 sourcePosition, Vector3 listenerPosition, in SpatialContext context);
}
