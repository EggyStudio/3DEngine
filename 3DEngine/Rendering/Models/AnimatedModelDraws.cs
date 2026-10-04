using System.Numerics;

namespace Engine;

/// <summary>
/// Loads, plays and draws each entity's <see cref="AnimatedModel"/>, through the flat API's
/// models and clips, so an entity is posed and drawn as <c>UpdateModelAnimationAt</c> and
/// <c>DrawModel</c> pose and draw a model.
/// </summary>
/// <remarks>
/// <para>
/// Runs in <see cref="Stage.Render"/>, in the app <c>InitWindow</c> built, whose models these are.
/// An entity's model is loaded the first frame it is seen, again when its path changes, and
/// unloaded the first frame the entity or its component is gone. A file that cannot be loaded is
/// tried once for that path, with the reason in the log.
/// </para>
/// <para>
/// A change of <see cref="AnimatedModel.Clip"/> keeps the clip before playing on beside it and
/// blends from it to the new one over <see cref="AnimatedModel.BlendSeconds"/>.
/// </para>
/// </remarks>
public sealed class AnimatedModelDraws
{
    private static readonly ILogger Logger = Log.Category("Engine.Models");

    // Each entity's model and what it is playing, by entity id, with the generation it was loaded
    // for, so an id given out again does not take over a model.
    private readonly Dictionary<int, Playing> _playing = [];
    private readonly HashSet<int> _seen = [];
    private bool _warned;

    private sealed class Playing
    {
        public required string Path;
        public required int Generation;
        public Model? Model;
        public ModelAnimation[] Clips = [];
        public string Clip = "";
        // The clip blended from after a change, how far into it, and how far the blend has gone.
        public ModelAnimation? From;
        public float FromTime;
        public float Blend;
        // The time the last frame played, which a change blends from, since the program may set
        // the time when it changes the clip.
        public float LastTime;
    }

    /// <summary>How many models are loaded for entities.</summary>
    public int ModelCount => _playing.Values.Count(p => p.Model is not null);

    /// <summary>The system, for <see cref="Stage.Render"/>.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;
        var self = world.GetOrInsertResource(static () => new AnimatedModelDraws());
        self.Record(world, ecs);
    }

    private void Record(World world, EcsWorld ecs)
    {
        _seen.Clear();
        if (ecs.Count<AnimatedModel>() > 0)
        {
            if (!Engine3D.Holds(world))
            {
                if (!_warned) Logger.Warn("AnimatedModel is drawn through the flat API, in the app InitWindow built, and this app is not that one.");
                _warned = true;
                return;
            }

            var dt = world.TryGetResource<Time>(out var time) ? (float)time.DeltaSeconds : 0f;
            var camera = MeshEntityDraws.FirstCamera(world, ecs);
            foreach (var (entity, animated) in ecs.Query<AnimatedModel>())
            {
                _seen.Add(entity);
                var playing = Load(ecs, entity, animated);
                if (playing.Model is not { } model) continue;

                ref var component = ref ecs.GetRef<AnimatedModel>(entity);
                Play(model, playing, ref component, dt);
                if (camera is { } view) Engine3D.RecordModel(model, TransformPropagation.WorldMatrix(ecs, entity), view.ViewProjection);
            }
        }

        if (_playing.Count > _seen.Count)
            foreach (var entity in _playing.Keys.Where(e => !_seen.Contains(e)).ToArray())
                Forget(entity);
    }

    // The entity's model, loaded when it has none for its path and generation.
    private Playing Load(EcsWorld ecs, int entity, in AnimatedModel animated)
    {
        var path = animated.Path ?? "";
        var generation = ecs.GetGeneration(entity);
        if (_playing.TryGetValue(entity, out var known) && known.Path == path && known.Generation == generation) return known;

        Forget(entity);
        var playing = new Playing { Path = path, Generation = generation };
        _playing[entity] = playing;
        if (path.Length == 0) return playing;

        var model = Engine3D.LoadModel(path);
        if (model.Meshes.Length == 0)
        {
            Logger.Warn($"AnimatedModel: '{path}' has no meshes to draw.");
            Engine3D.UnloadModel(model);
            return playing;
        }
        playing.Model = model;
        playing.Clips = Engine3D.LoadModelAnimations(path);
        return playing;
    }

    // Advances the clip and poses the model, blending from the clip before after a change.
    private static void Play(Model model, Playing playing, ref AnimatedModel animated, float dt)
    {
        if (playing.Clips.Length == 0) return;
        var clip = Find(playing.Clips, animated.Clip);
        if (clip is null) return;

        var name = animated.Clip ?? "";
        if (name != playing.Clip)
        {
            var before = Find(playing.Clips, playing.Clip);
            // The first clip played, or one cut to, starts with nothing to blend from.
            playing.From = animated.BlendSeconds > 0 && before is not null && before != clip ? before : null;
            playing.FromTime = playing.LastTime;
            playing.Blend = 0;
            playing.Clip = name;
        }

        var step = animated.Speed * dt;
        animated.Time += step;
        playing.LastTime = animated.Time;
        if (playing.From is { } from)
        {
            playing.FromTime += step;
            playing.Blend += dt / animated.BlendSeconds;
            if (playing.Blend < 1)
            {
                Engine3D.UpdateModelAnimationBlend(model, from, playing.FromTime, clip, animated.Time, playing.Blend);
                return;
            }
            playing.From = null;
        }
        Engine3D.UpdateModelAnimationAt(model, clip, animated.Time);
    }

    // A clip by name, or the first for an empty name, or null for a name the file does not have.
    private static ModelAnimation? Find(ModelAnimation[] clips, string? name) =>
        string.IsNullOrEmpty(name) ? clips.FirstOrDefault() : clips.FirstOrDefault(c => c.Name == name);

    private void Forget(int entity)
    {
        if (!_playing.Remove(entity, out var playing)) return;
        if (playing.Model is { } model) Engine3D.UnloadModel(model);
    }
}
