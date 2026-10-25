using System.Numerics;

namespace Engine;

/// <summary>
/// An emitter of particles at its entity's place, as smoke, sparks or dust, a stream of small
/// squares that face the camera, or a way of their own, born at a rate, moving and falling, and changing size and color
/// over their lives, slowed by the air, simulated on the GPU and drawn into the window and render textures.
/// </summary>
/// <remarks>
/// <para>
/// Each emitter keeps up to <see cref="MaxParticles"/> on the GPU, which a compute shader steps
/// every frame, those that may still be alive alone, so an emitter whose particles have died costs
/// nothing until it emits again and one with a few alive costs its few. A particle starts within <see cref="Radius"/> of the entity, with
/// <see cref="Velocity"/> turned by up to <see cref="Spread"/> degrees and its speed and life
/// varied by <see cref="SpeedVariation"/> and <see cref="LifeVariation"/>, falls by
/// <see cref="Gravity"/>, slows by <see cref="Drag"/>, and goes from <see cref="StartSize"/> and <see cref="StartColor"/> to
/// <see cref="EndSize"/> and <see cref="EndColor"/>. Once every slot is taken the oldest particle
/// is the one replaced. A particle keeps its place in the world once born, or with
/// <see cref="Local"/> lives in the entity's space and moves and turns with it, and the program
/// may place particles of its own at <see cref="Points"/> in that space, which stand there.
/// </para>
/// <para>
/// An unlit particle emits its color times <see cref="Intensity"/>, which blooms where it
/// passes 1, and a lit one is lit by the scene's lights as a rough surface facing the camera.
/// Particles are drawn after the window's meshes, with depth tested and not written, through the
/// window's camera, and into a render texture after its meshes, through the camera of its first
/// <c>BeginMode3D</c> or of the camera entity drawing it, so one drawn only in 2D has none. A
/// probe's capture has none. Emitters laid over by
/// alpha are drawn from the farthest from the camera to the nearest, so where two overlap the
/// nearer is in front, and the particles within one are sorted the same way on the GPU each frame,
/// from the window's camera.
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

    /// <summary>
    /// How strongly the air slows a particle, 0 for not at all and 1 leaving about a third of its
    /// speed after a second, so smoke puffed out slows and drifts where sparks fly on.
    /// </summary>
    /// <remarks>With <see cref="Gravity"/> it falls or rises no faster than gravity over drag.</remarks>
    public float Drag;

    /// <summary>How wide a particle is at birth, in world units.</summary>
    public float StartSize;

    /// <summary>How wide a particle is at the end of its life.</summary>
    public float EndSize;

    /// <summary>Its color and alpha at birth.</summary>
    public Color StartColor;

    /// <summary>Its color and alpha at the end of its life, transparent for one that fades out.</summary>
    public Color EndColor;

    /// <summary>What an unlit particle's color is multiplied by, past 1 for one that blooms.</summary>
    public float Intensity;

    /// <summary>Whether the scene's lights light it, rather than it giving off its own color.</summary>
    public bool Lit;

    /// <summary>How it is laid over what is behind it.</summary>
    public ParticleBlend Blend;

    /// <summary>Which way each particle's square faces, the camera unless set.</summary>
    /// <remarks>
    /// Facing <see cref="Normal"/> or its velocity, a particle's square lies across that way in the
    /// space its particles are kept in, the world's or with <see cref="Local"/> the entity's, its
    /// texture and a sheet's frames drawn on it as on one facing the camera, and lit as a surface
    /// facing that way, from either side. A ring a raindrop leaves on water faces up, which a ring
    /// facing the camera would stand up off the water to show.
    /// </remarks>
    public ParticleFacing Facing;

    /// <summary>The way particles face where <see cref="Facing"/> is <see cref="ParticleFacing.Normal"/>, up where it is left zero.</summary>
    public Vector3 Normal;

    /// <summary>
    /// An image each particle is drawn as, a puff of smoke or a flame, tinted by its color, in place
    /// of the round soft dot. Unset for the dot.
    /// </summary>
    /// <remarks>
    /// The texture is the program's, loaded with <c>LoadTexture</c>, so a scene file does not hold
    /// it, and an emitter a scene places is given its texture by the program.
    /// </remarks>
    public Texture2D Texture;

    /// <summary>
    /// How many frames across <see cref="Texture"/> holds, as a sheet played through over each
    /// particle's life, left to right and then down a row. 0 or 1 for an image drawn whole.
    /// </summary>
    public int TextureColumns;

    /// <summary>How many rows of frames <see cref="Texture"/> holds, 0 or 1 for one.</summary>
    public int TextureRows;

    /// <summary>
    /// Whether a sheet's frames blend from one into the next over each frame's share of a life, as
    /// smoke that rolls is drawn smooth from a few frames, in place of each frame shown whole until
    /// the next replaces it, as a sheet of drawn frames is meant to be seen.
    /// </summary>
    public bool BlendFrames;

    /// <summary>
    /// Whether its particles live in its entity's space, moving and turning with its transform and
    /// its parent's, rather than each keeping its place in the world from its birth on.
    /// </summary>
    /// <remarks>
    /// They are stepped in that space, so <see cref="Velocity"/> and <see cref="Gravity"/> turn with
    /// the entity too, and the entity's world matrix places them where they are drawn, their size
    /// kept in world units. They do not meet the scene, whatever <see cref="Collision"/> says.
    /// </remarks>
    public bool Local;

    /// <summary>
    /// Particles the program places, one at each point, in the entity's space, so they move and turn
    /// with it, standing still there and living until the array no longer holds them, drawn at
    /// <see cref="StartSize"/> in <see cref="StartColor"/>. Null for none.
    /// </summary>
    /// <remarks>
    /// They are drawn beside the particles born at <see cref="Rate"/>, which <see cref="MaxParticles"/>
    /// counts and they do not. An array changed, in place or for another, is read again the frame
    /// after, its points sent to the GPU whole, so a sky of stars is placed once and left.
    /// </remarks>
    public Vector3[]? Points;

    /// <summary>Particles to emit at once in the next frame, beside the rate, which drawing them clears.</summary>
    public int Burst;

    /// <summary>
    /// What the particles do where they meet the scene, bounce off or end there, which they do not
    /// unless set.
    /// </summary>
    /// <remarks>
    /// They meet the depth of the window's meshes that cast shadows, drawn at half its size through
    /// its camera before they are stepped, so they pass through what the window does not show, what
    /// is off screen or behind something, and through shapes drawn without a model. A particle
    /// meets a surface where it has gone behind it by no more than it moved in the frame, so one
    /// faster than a surface is thick may pass through it.
    /// </remarks>
    public ParticleCollision Collision;

    /// <summary>The share of its speed into a surface a particle keeps, back out of it, when it bounces, from 0 to 1.</summary>
    public float Bounce;

    /// <summary>
    /// A small white fountain of 500 particles, 50 a second, living two seconds, rising at 3 units
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
        Bounce = 0.5f,
    };
}
