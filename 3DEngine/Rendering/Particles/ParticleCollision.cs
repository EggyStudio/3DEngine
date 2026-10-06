namespace Engine;

/// <summary>What an emitter's particles do where they meet the scene, as the window's depth of its meshes shows it.</summary>
public enum ParticleCollision
{
    /// <summary>They go through everything, as they do unless set.</summary>
    None,

    /// <summary>They bounce off, keeping <see cref="ParticleEmitter.Bounce"/> of their speed into the surface, as sparks off the ground.</summary>
    Bounce,

    /// <summary>Their lives end there, as rain on a roof.</summary>
    Die,
}
