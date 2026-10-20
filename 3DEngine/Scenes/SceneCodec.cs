using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>A codec made of the two functions the generator writes for a component type.</summary>
public sealed class SceneCodec<T>(string name, SceneCodec<T>.Writer write, SceneCodec<T>.Reader read) : ISceneCodec, ITellsLoaded
{
    /// <summary>Writes a component's fields into the open object.</summary>
    public delegate void Writer(Utf8JsonWriter writer, in T value, SceneWriteContext context);

    /// <summary>Reads a component from an object of its fields.</summary>
    public delegate T Reader(JsonElement element, SceneReadContext context);

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public Type Type => typeof(T);

    /// <inheritdoc />
    public IReadOnlyList<string> FormerNames { get; init; } = [];

    /// <inheritdoc />
    public int Version { get; init; } = 1;

    /// <inheritdoc />
    public bool Has(EcsWorld ecs, int entity) => ecs.Has<T>(entity);

    /// <inheritdoc />
    public void Write(Utf8JsonWriter writer, EcsWorld ecs, int entity, SceneWriteContext context)
    {
        if (ecs.TryGet<T>(entity, out var value)) write(writer, value!, context);
    }

    /// <inheritdoc />
    public void Read(JsonElement element, EcsWorld ecs, int entity, SceneReadContext context)
    {
        var value = read(element, context);
        if (ecs.Has<T>(entity)) ecs.Update(entity, value);
        else ecs.Add(entity, value);
    }

    bool ITellsLoaded.Watched(EcsWorld ecs) => ecs.CountsLoads<T>();

    void ITellsLoaded.Tell(EcsWorld ecs, int entity)
    {
        if (ecs.TryGet<T>(entity, out var value)) ecs.Trigger(new Loaded<T>(ecs.Handle(entity), value!));
    }
}

/// <summary>A codec that tells the observers of its component on an entity a scene file spawned.</summary>
internal interface ITellsLoaded
{
    /// <summary>Whether anything observes the component loaded.</summary>
    bool Watched(EcsWorld ecs);

    /// <summary>Triggers the component's <see cref="Loaded{T}"/> on the entity, where it has one.</summary>
    void Tell(EcsWorld ecs, int entity);
}
