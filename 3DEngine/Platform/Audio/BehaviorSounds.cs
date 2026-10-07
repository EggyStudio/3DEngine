using System.Numerics;

namespace Engine;

/// <summary>Sounds a behavior plays in the world, heard from where the entity with an <see cref="AudioListener"/> is.</summary>
public static class BehaviorSounds
{
    /// <summary>
    /// Loads the sound file at <paramref name="path"/> and plays it at <paramref name="position"/>,
    /// louder near the listener and heard from its side, giving the voice to move later.
    /// </summary>
    /// <remarks>
    /// The listener is the entity with an <see cref="AudioListener"/> component, usually the camera.
    /// The voice's <see cref="AudioSource.SetPosition"/> moves the sound as what makes it moves, as
    /// a truck's engine follows the truck.
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = ctx.PlaySpatialSound("resources/drone.ogg", truckPosition);
    /// // ...
    /// engine.SetPosition(truckPosition);
    /// </code>
    /// </example>
    public static AudioSource PlaySpatialSound(this BehaviorContext ctx, string path, Vector3 position, AudioVoiceParams parameters = default) =>
        ctx.World.PlaySpatialSound(path, position, parameters);
}
