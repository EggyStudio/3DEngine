
namespace Engine;

/// <summary>
/// A system, which receives the <see cref="World"/> to read and write resources and entities.
/// This is the fundamental unit of work in the ECS schedule.
/// </summary>
/// <param name="world">The shared <see cref="World"/> containing all resources and entity data.</param>
/// <seealso cref="SystemDescriptor"/>
/// <seealso cref="Schedule"/>
public delegate void SystemFn(World world);
