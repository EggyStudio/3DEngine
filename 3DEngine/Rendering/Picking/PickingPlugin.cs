namespace Engine;

/// <summary>
/// What the mouse does to the mesh entities a picking camera draws, triggered on them before the
/// game's update, on the main thread, since an observer may change anything. <see cref="DefaultPlugins"/>
/// adds it between input and the ECS, so its system runs after input's and before the behaviors'.
/// </summary>
internal sealed class PickingPlugin : IPlugin
{
    public void Build(App app)
    {
        app.World.InsertResource(new PointerPicking());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(PointerPicking.Run, "PickingPlugin.PointerPicking").MainThreadOnly());
    }
}
