using System.Numerics;

namespace Engine;

/// <summary>How an emitter's particles are laid over what is behind them.</summary>
public enum ParticleBlend
{
    /// <summary>Their light added to what is behind, as fire, sparks and magic are, which needs no order.</summary>
    Additive,

    /// <summary>Laid over what is behind by their alpha, as smoke and dust are, in the order they were born.</summary>
    Alpha,
}

/// <summary>
/// An emitter of particles at its entity's place, as smoke, sparks or dust: a stream of small
/// squares that face the camera, born at a rate, moving and falling, and changing size and color
/// over their lives, simulated on the GPU and drawn into the window.
/// </summary>
/// <remarks>
/// <para>
/// Each emitter keeps up to <see cref="MaxParticles"/> on the GPU, which a compute shader steps
/// every frame. A particle starts within <see cref="Radius"/> of the entity, with
/// <see cref="Velocity"/> turned by up to <see cref="Spread"/> degrees and its speed and life
/// varied by <see cref="SpeedVariation"/> and <see cref="LifeVariation"/>, falls by
/// <see cref="Gravity"/>, and goes from <see cref="StartSize"/> and <see cref="StartColor"/> to
/// <see cref="EndSize"/> and <see cref="EndColor"/>. Once every slot is taken the oldest particle
/// is the one replaced.
/// </para>
/// <para>
/// An unlit particle gives off its color times <see cref="Intensity"/>, which blooms where it
/// passes 1, and a lit one is lit by the scene's lights as a rough surface facing the camera.
/// Particles are drawn after the window's meshes, with depth tested and not written, through the
/// window's camera, and not into render textures or a probe's capture. Alpha-blended particles
/// are not sorted by distance, so where two emitters overlap the later drawn is in front.
/// </para>
/// </remarks>
[SceneComponent]
public struct ParticleEmitter
{
    /// <summary>The most particles alive at once, which the GPU keeps room for.</summary>
    public int MaxParticles;

    /// <summary>How many are born each second while <see cref="Emitting"/>.</summary>
    public float Rate;

    /// <summary>Whether it is giving off particles at its <see cref="Rate"/>, which a burst does not need.</summary>
    public bool Emitting;

    /// <summary>How many seconds each lives.</summary>
    public float Life;

    /// <summary>The share by which a life is longer or shorter than <see cref="Life"/>, from 0 to 1.</summary>
    public float LifeVariation;

    /// <summary>How fast and which way a particle starts, in units a second.</summary>
    public Vector3 Velocity;

    /// <summary>How far, in degrees, a particle's way may turn from <see cref="Velocity"/>'s, 180 for any way at all.</summary>
    public float Spread;

    /// <summary>The share by which a speed is faster or slower than <see cref="Velocity"/>'s, from 0 to 1.</summary>
    public float SpeedVariation;

    /// <summary>How far from the entity a particle may start.</summary>
    public float Radius;

    /// <summary>The pull on every particle, in units a second squared, down for sparks and up for smoke.</summary>
    public Vector3 Gravity;

    /// <summary>How wide a particle is at birth, in world units.</summary>
    public float StartSize;

    /// <summary>How wide a particle is at the end of its life.</summary>
    public float EndSize;

    /// <summary>Its color and alpha at birth.</summary>
    public Color StartColor;

    /// <summary>Its color and alpha at the end of its life, transparent for one that fades out.</summary>
    public Color EndColor;

    /// <summary>What an unlit particle's color is multiplied by, past 1 for one that glows through bloom.</summary>
    public float Intensity;

    /// <summary>Whether the scene's lights light it, rather than it giving off its own color.</summary>
    public bool Lit;

    /// <summary>How it is laid over what is behind it.</summary>
    public ParticleBlend Blend;

    /// <summary>Particles to give off at once in the next frame, beside the rate, which drawing them clears.</summary>
    public int Burst;

    /// <summary>
    /// A small white fountain: 500 particles, 50 a second, living two seconds, rising at 3 units
    /// a second within 20 degrees of up and falling back, shrinking from 0.2 to 0.05 and fading out.
    /// </summary>
    public static ParticleEmitter Default => new()
    {
        MaxParticles = 500,
        Rate = 50,
        Emitting = true,
        Life = 2,
        LifeVariation = 0.2f,
        Velocity = new Vector3(0, 3, 0),
        Spread = 20,
        SpeedVariation = 0.2f,
        Gravity = new Vector3(0, -2, 0),
        StartSize = 0.2f,
        EndSize = 0.05f,
        StartColor = Color.White,
        EndColor = Color.White with { A = 0 },
        Intensity = 1,
        Blend = ParticleBlend.Additive,
    };
}
