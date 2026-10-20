using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>Saves a component of one type into a scene file's entry and loads it back.</summary>
public interface ISceneCodec
{
    /// <summary>The key the component is written under, its type's name.</summary>
    string Name { get; }

    /// <summary>The component type.</summary>
    Type Type { get; }

    /// <summary>
    /// The full names the component was written under before, from its type's
    /// <see cref="FormerNameAttribute"/>, which a file is read from where no registered type now has
    /// the name. None, the default, where it was never renamed.
    /// </summary>
    IReadOnlyList<string> FormerNames => [];

    /// <summary>
    /// The version of the layout the component is saved in, from its type's
    /// <see cref="DataVersionAttribute"/>, 1 where it has none.
    /// </summary>
    int Version => 1;

    /// <summary>Whether the entity has the component.</summary>
    bool Has(EcsWorld ecs, int entity);

    /// <summary>Writes the entity's component's fields into the object the caller has opened for it.</summary>
    void Write(Utf8JsonWriter writer, EcsWorld ecs, int entity, SceneWriteContext context);

    /// <summary>Reads a component from an object of its fields and adds it to the entity, replacing one it has.</summary>
    void Read(JsonElement element, EcsWorld ecs, int entity, SceneReadContext context);
}
