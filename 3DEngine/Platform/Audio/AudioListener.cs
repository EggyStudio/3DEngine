using System.Numerics;

namespace Engine;

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
