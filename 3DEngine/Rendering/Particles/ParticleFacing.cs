namespace Engine;

/// <summary>Which way an emitter's particles face, the square each is drawn as lying across it.</summary>
public enum ParticleFacing
{
    /// <summary>Toward the camera, as smoke, sparks and dust do, the same from every side.</summary>
    Camera,

    /// <summary>Along the emitter's <see cref="ParticleEmitter.Normal"/>, as a ring on water lies flat facing up and is seen edge-on from beside it.</summary>
    Normal,

    /// <summary>The way each particle moves, as a shock wave's ring faces where it travels, toward the camera while it stands still.</summary>
    Velocity,
}
