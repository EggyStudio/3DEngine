using System.Numerics;

namespace Engine;

/// <summary>
/// Convenience helpers that collapse the standard "load a <see cref="Sound"/> through
/// the <see cref="AssetServer"/> + create a voice on the <see cref="AudioServer"/>"
/// boilerplate into single calls. Mirrors <see cref="TextureLoadExtensions"/> /
/// <see cref="SceneSpawnExtensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Async-safe:</b> all <c>PlaySound</c> / <c>PlaySpatialSound</c> overloads return
/// immediately with a valid <see cref="AudioSource"/> ticket even when the asset is
/// still streaming in. <see cref="AudioServer.ResolvePending"/> finishes the wiring on
/// the first frame the asset becomes available.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // From a behavior (non-spatial 2D playback):
/// var ui = ctx.PlaySound("ui/click.wav");
///
/// // Spatial:
/// var music = ctx.PlaySpatialSound("rock_music.wav", new Vector3(5, 0, 0));
/// music.SetVolume(0.6f);
/// music.SetLooping(true);
/// </code>
/// </example>
public static class AudioLoadExtensions
{
    // -- Asset loading shortcuts (no playback) --

    /// <summary>Loads a sound asset. Shorthand for <c>server.Load&lt;Sound&gt;(path)</c>.</summary>
    public static Handle<Sound> LoadSound(this AssetServer server, string path) =>
        server.Load<Sound>(path);

    /// <summary>Loads a sound through the world's <see cref="AssetServer"/>.</summary>
    public static Handle<Sound> LoadSound(this World world, string path) =>
        world.Resource<AssetServer>().Load<Sound>(path);

    /// <summary>Loads a sound through the behavior context's world.</summary>
    public static Handle<Sound> LoadSound(this BehaviorContext ctx, string path) =>
        ctx.World.Resource<AssetServer>().Load<Sound>(path);

    // -- ctx.Audio() shortcut (extension methods can't be properties; closest match to ctx.Audio) --

    /// <summary>Returns the active <see cref="AudioServer"/> resource.</summary>
    public static AudioServer Audio(this World world) =>
        world.Resource<AudioServer>();

    /// <summary>Returns the active <see cref="AudioServer"/> resource.</summary>
    public static AudioServer Audio(this BehaviorContext ctx) =>
        ctx.World.Resource<AudioServer>();

    // -- One-call play helpers (load + play) --

    /// <summary>Loads <paramref name="path"/> and starts a non-spatial 2D voice.</summary>
    public static AudioSource PlaySound(this World world, string path, AudioVoiceParams parameters = default)
    {
        var server = world.Resource<AudioServer>();
        var assets = world.Resource<Assets<Sound>>();
        var handle = world.Resource<AssetServer>().Load<Sound>(path);
        return server.Play(handle, assets, parameters);
    }

    /// <inheritdoc cref="PlaySound(World, string, AudioVoiceParams)"/>
    public static AudioSource PlaySound(this BehaviorContext ctx, string path, AudioVoiceParams parameters = default) =>
        ctx.World.PlaySound(path, parameters);

    /// <summary>Loads <paramref name="path"/> and starts a 3D voice at <paramref name="position"/>.</summary>
    public static AudioSource PlaySpatialSound(this World world, string path, Vector3 position, AudioVoiceParams parameters = default)
    {
        var server = world.Resource<AudioServer>();
        var assets = world.Resource<Assets<Sound>>();
        var handle = world.Resource<AssetServer>().Load<Sound>(path);
        return server.PlaySpatial(handle, assets, position, parameters);
    }

    /// <inheritdoc cref="PlaySpatialSound(World, string, Vector3, AudioVoiceParams)"/>
    public static AudioSource PlaySpatialSound(this BehaviorContext ctx, string path, Vector3 position, AudioVoiceParams parameters = default) =>
        ctx.World.PlaySpatialSound(path, position, parameters);

    // -- AudioServer-level convenience for the user-illustrated CreateSpatialSource / CreateSource shape --

    /// <summary>
    /// Creates a 3D voice at the world origin from <paramref name="path"/>. Matches the
    /// shape of the user's <c>ctx.Audio.CreateSpatialSource("foo.wav")</c> illustration:
    /// the source is created spatial-by-default, ready for a follow-up
    /// <see cref="AudioSource.SetPosition"/>.
    /// </summary>
    public static AudioSource CreateSpatialSource(this AudioServer server, World world, string path, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        var assets = world.Resource<Assets<Sound>>();
        var handle = world.Resource<AssetServer>().Load<Sound>(path);
        return server.PlaySpatial(handle, assets, Vector3.Zero, parameters);
    }

    /// <summary>Non-spatial counterpart to <see cref="CreateSpatialSource(AudioServer, World, string, AudioVoiceParams)"/>.</summary>
    public static AudioSource CreateSource(this AudioServer server, World world, string path, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        var assets = world.Resource<Assets<Sound>>();
        var handle = world.Resource<AssetServer>().Load<Sound>(path);
        return server.Play(handle, assets, parameters);
    }
}