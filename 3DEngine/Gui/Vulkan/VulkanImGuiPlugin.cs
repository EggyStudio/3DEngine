namespace Engine;

/// <summary>
/// Plugin that wires ImGui rendering into the Vulkan render graph.
/// Only activates when the graphics backend is Vulkan.
/// </summary>
/// <remarks>
/// Registers a <see cref="Stage.Startup"/> system that adds an <see cref="ImGuiRenderNode"/>
/// to the <see cref="Renderer"/>'s render graph.  If the graphics backend is not Vulkan,
/// the plugin is a no-op.
/// </remarks>
/// <seealso cref="ImGuiRenderNode"/>
public sealed class VulkanImGuiPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.ImGui.Vulkan");

    /// <inheritdoc />
    public void Build(App app)
    {
        var cfg = app.World.Resource<Config>();
        if (cfg.Graphics != GraphicsBackend.Vulkan)
        {
            Logger.Info("VulkanImGuiPlugin: Not a Vulkan backend, so there is nothing to add.");
            return;
        }

        Logger.Info("VulkanImGuiPlugin: Adding the ImGui render node to the Vulkan graph.");

        app.AddSystem(Stage.Startup, new SystemDescriptor(world =>
            {
                if (!world.TryGetResource<Renderer>(out var renderer) || !renderer.Context.IsInitialized)
                {
                    Logger.Info("No initialized renderer (a headless run), so the ImGui render node is not added.");
                    return;
                }

                // Load shaders via AssetServer at startup
                var server = world.Resource<AssetServer>();
                var shader = server.LoadSync<ShaderProgram>("shaders/imgui.slang");

                renderer.Graph.AddNode("imgui", new ImGuiRenderNode(shader.Vertex, shader.Fragment));
                renderer.Graph.AddNodeEdge("main_pass", "imgui");
                if (renderer.Graph.ContainsNode("immediate"))
                    renderer.Graph.AddNodeEdge("immediate", "imgui");

                Logger.Info("ImGuiRenderNode registered in render graph (after 'main_pass').");
            }, "VulkanImGuiPlugin.Startup")
            .MainThreadOnly()
            .Write<Renderer>());

        Logger.Info("VulkanImGuiPlugin: Build complete.");
    }
}
