namespace Engine;

/// <summary>A change to one type of component that the world's store of it counts the observers of.</summary>
internal interface IComponentEvent
{
    /// <summary>Counts <paramref name="by"/> more observers of this change in <paramref name="ecs"/>.</summary>
    void Count(EcsWorld ecs, int by);
}
