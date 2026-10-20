using System.Numerics;

namespace Engine;

/// <summary>Something the mouse did to an entity a picking camera draws, as Bevy's <c>Pointer&lt;E&gt;</c>.</summary>
/// <typeparam name="TEvent">What it did, one of <see cref="Over"/>, <see cref="Click"/>, <see cref="Drag"/> and the rest.</typeparam>
/// <param name="Entity">The entity it did it to, the nearest one under the pointer, or the one a drag started on.</param>
/// <param name="Position">Where the pointer is, in window pixels from the top left corner.</param>
/// <param name="Event">What it did, and with what.</param>
/// <remarks>
/// <para>
/// Observed as any entity event is, at one entity or at every one, with
/// <see cref="EcsWorld.Observe{TEvent}(Entity, Action{On{TEvent}})"/>. It goes on up the entity's
/// parents after the entity's own observers, so a model's root hears a click on any of its meshes,
/// until an observer calls <see cref="On{TEvent}.Propagate"/> with false.
/// </para>
/// <para>
/// Triggered where a <see cref="Camera"/> drawing into the window has <see cref="Camera.Picking"/>
/// set. Each frame the pointer's ray through it is cast at every <see cref="Mesh"/> entity, by the
/// sphere around its positions and then their triangles, and the nearest it meets is the one under
/// the pointer. Nothing is cast where no camera asks.
/// </para>
/// </remarks>
public readonly record struct Pointer<TEvent>(Entity Entity, Vector2 Position, TEvent Event) : IPropagatingEvent
    where TEvent : struct, IPointerEvent;
