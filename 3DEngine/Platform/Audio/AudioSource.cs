using System.Numerics;

namespace Engine;

/// <summary>
/// Lightweight gameplay handle to a playing (or pending) voice. Stable across the
/// async asset-load handoff: synchronous calls like <c>ctx.PlaySpatialSound("a.wav", pos)</c>
/// always return a valid <see cref="AudioSource"/>; the actual mixer voice is created
/// later by <see cref="AudioServer.ResolvePending"/> when the <see cref="Sound"/>
/// finishes loading. All operations route through the owning <see cref="AudioServer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Safe-by-default:</b> the default value (<c>default(AudioSource)</c>) is invalid -
/// every method becomes a no-op. Stored components can leave the field as <c>default</c>
/// before the source is created without null-checking.
/// </para>
/// <para>
/// <b>Equality:</b> identity is the <see cref="Id"/> ticket alone; two handles with the
/// same ticket but different server references compare unequal.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Behavior]
/// public partial struct Radio
/// {
///     public AudioSource Music;
///
///     [OnStartup]
///     public static void Spawn(BehaviorContext ctx)
///     {
///         var e = ctx.Ecs.Spawn();
///         var music = ctx.PlaySpatialSound("rock_music.wav", Vector3.Zero);
///         ctx.Ecs.Add(e, new Radio { Music = music });
///     }
///
///     [OnUpdate]
///     public void Tick(BehaviorContext ctx)
///     {
///         Music.SetPosition(Vector3.Zero); // forwarded to backend + spatial processor
///     }
/// }
/// </code>
/// </example>
public readonly struct AudioSource : IEquatable<AudioSource>
{
    /// <summary>Server-issued ticket id. <c>0</c> = invalid (default-constructed).</summary>
    public int Id { get; }

    /// <summary>Server that owns this voice. May be <c>null</c> for the default value.</summary>
    public AudioServer? Server { get; }

    /// <summary>True when both <see cref="Id"/> and <see cref="Server"/> are present.</summary>
    public bool IsValid => Id != 0 && Server is not null;

    /// <summary>Returns an invalid / default source.</summary>
    public static AudioSource Invalid => default;

    internal AudioSource(int id, AudioServer server)
    {
        Id = id;
        Server = server;
    }

    /// <summary>Updates the spatial position. No-op when invalid; converts non-spatial voices to spatial.</summary>
    public void SetPosition(Vector3 position)
    {
        if (!IsValid) return;
        Server!.SetPosition(Id, position);
    }

    /// <summary>Sets per-voice linear gain.</summary>
    public void SetVolume(float volume)
    {
        if (!IsValid) return;
        Server!.SetVolume(Id, volume);
    }

    /// <summary>Toggles the loop flag.</summary>
    public void SetLooping(bool looping)
    {
        if (!IsValid) return;
        Server!.SetLooping(Id, looping);
    }

    /// <summary>Pauses or resumes the voice.</summary>
    public void SetPaused(bool paused)
    {
        if (!IsValid) return;
        Server!.SetPaused(Id, paused);
    }

    /// <summary>Stops and releases the voice. Subsequent calls are no-ops.</summary>
    public void Stop()
    {
        if (!IsValid) return;
        Server!.Stop(Id);
    }

    /// <summary>
    /// Sets the source's world-space orientation. When non-default, the spatial
    /// processor uses it to compute directivity attenuation (e.g. cardioid sources
    /// like a bullhorn). Engine convention: rotated <c>-Z</c> = "front" of the source.
    /// </summary>
    public void SetOrientation(Quaternion orientation)
    {
        if (!IsValid) return;
        Server!.SetOrientation(Id, orientation);
    }

    /// <summary>
    /// Sets the source's directivity pattern. <paramref name="dipoleWeight"/> in
    /// <c>[0, 1]</c> blends omni (0) and pure dipole (1); <paramref name="dipolePower"/>
    /// (≥ 0) sharpens the front lobe. Has no effect until
    /// <see cref="SetOrientation"/> has been called.
    /// </summary>
    public void SetDirectivity(float dipoleWeight, float dipolePower = 1f)
    {
        if (!IsValid) return;
        Server!.SetDirectivity(Id, dipoleWeight, dipolePower);
    }

    /// <summary>
    /// Sets the playback-rate multiplier (also known as pitch ratio). <c>1.0</c> is
    /// native pitch / native speed; <c>2.0</c> doubles speed (one octave up); <c>0.5</c>
    /// halves it (one octave down). Backends typically clamp to a safe range (SDL3:
    /// <c>[0.01, 100]</c>). Useful for footsteps variation, vehicle engines, or
    /// gameplay-driven Doppler.
    /// </summary>
    public void SetPlaybackRate(float rate)
    {
        if (!IsValid) return;
        Server!.SetPlaybackRate(Id, rate);
    }

    /// <summary>True while the voice is mixing (or pending an asset-load resolve).</summary>
    public bool IsPlaying
    {
        get
        {
            if (!IsValid) return false;
            return Server!.IsPlaying(Id);
        }
    }

    /// <inheritdoc />
    public bool Equals(AudioSource other) => Id == other.Id && ReferenceEquals(Server, other.Server);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AudioSource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(AudioSource left, AudioSource right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(AudioSource left, AudioSource right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsValid ? $"AudioSource({Id})" : "AudioSource(invalid)";
}

/// <summary>
/// Marker component identifying an entity as the active audio listener. The
/// <see cref="AudioListenerSystem"/> copies this entity's <see cref="Transform.Position"/>
/// into <see cref="AudioServer.ListenerPosition"/> each frame so 3D voice attenuation
/// tracks the camera / player.
/// </summary>
/// <remarks>
/// Only one listener entity is honoured per frame; if multiple are present the first
/// query result wins. Convention: attach to the camera entity.
/// </remarks>
public struct AudioListener
{
    /// <summary>Master volume multiplier applied to every voice. <c>1.0</c> = unity.</summary>
    public float MasterVolume;

    /// <summary>Convenience factory: a listener at unity master volume.</summary>
    public static AudioListener Default => new() { MasterVolume = 1f };
}