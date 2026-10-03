namespace Engine;

/// <summary>
/// Per-frame system that pumps the <see cref="AudioServer"/>: resolves any pending
/// voices whose <see cref="Sound"/> assets finished loading this frame, then calls
/// <see cref="AudioServer.Tick"/> so the spatial processor and backend can refresh
/// their state.
/// </summary>
/// <remarks>
/// Registered by <see cref="SoundsPlugin"/> in <see cref="Stage.PostUpdate"/>, after
/// gameplay systems have set positions on their <see cref="AudioSource"/> handles.
/// </remarks>
public static class AudioUpdateSystem
{
    /// <summary>Runs the audio update for the current frame.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<AudioServer>(out var server)) return;
        if (world.TryGetResource<Assets<Sound>>(out var assets))
            server.ResolvePending(assets);
        server.Tick();
    }
}

/// <summary>
/// Copies the active <see cref="AudioListener"/> entity's <see cref="Transform.Position"/>
/// into <see cref="AudioServer.ListenerPosition"/> each frame so 3D voices stay anchored
/// to the player / camera.
/// </summary>
/// <remarks>
/// Picks the first <c>(Transform, AudioListener)</c> tuple in query order. If no listener
/// entity is present, the server's existing listener position is left unchanged - useful
/// for headless / editor setups where audio is muted.
/// </remarks>
public static class AudioListenerSystem
{
    /// <summary>Pulls the listener position from ECS into the audio server.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<AudioServer>(out var server)) return;
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;

        foreach (var (_, t, listener) in ecs.Query<Transform, AudioListener>())
        {
            server.ListenerPosition = t.Position;
            server.ListenerOrientation = t.Rotation;
            // MasterVolume is the listener-controlled global gain (e.g. an options
            // slider can write straight to the listener's component); AudioServer.Tick
            // folds it into every voice's per-frame output volume.
            server.MasterVolume = listener.MasterVolume;
            return; // first listener wins.
        }
    }
}