using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>A codec made of the two functions the generator writes for a component type.</summary>
public sealed class SceneCodec<T>(string name, SceneCodec<T>.Writer write, SceneCodec<T>.Reader read) : ISceneCodec
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
}
