using Engine.Examples;

// Each example is a program of its own, in the manner of raylib's examples, and this file picks
// one by name: dotnet run --project 3DEngine.Examples -- core_3d_camera_free
var examples = new Dictionary<string, Action>
{
    ["core_basic_window"] = CoreBasicWindow.Run,
    ["core_3d_camera_free"] = Core3DCameraFree.Run,
    ["core_input_gamepad"] = CoreInputGamepad.Run,
    ["shapes_basic_2d"] = ShapesBasic2D.Run,
    ["shapes_basic_3d"] = ShapesBasic3D.Run,
    ["textures_basic"] = TexturesBasic.Run,
    ["textures_render_target"] = TexturesRenderTarget.Run,
    ["models_loading"] = ModelsLoading.Run,
    ["shaders_postprocessing"] = ShadersPostprocessing.Run,
    ["text_fonts"] = TextFonts.Run,
    ["audio_sound"] = AudioSound.Run,
    ["gui_imgui_window"] = GuiImGuiWindow.Run,
    ["ecs_behaviors"] = EcsBehaviors.Run,
    ["ecs_mesh_entities"] = EcsMeshEntities.Run,
    ["ecs_states"] = EcsStates.Run,
};

var name = args.Length > 0 ? args[0] : "core_3d_camera_free";
if (!examples.TryGetValue(name, out var run))
{
    Console.WriteLine($"No example called '{name}'. The examples are:");
    foreach (var known in examples.Keys) Console.WriteLine($"  {known}");
    return 1;
}

Example.Current = name;
run();
return 0;

namespace Engine.Examples
{
    /// <summary>Which example is running, so an example's behaviors run only in that example.</summary>
    /// <remarks>
    /// Behaviors are discovered across the whole assembly, and every example lives in this one, so
    /// each behavior here carries a <c>[RunIf]</c> that asks this.
    /// </remarks>
    public static class Example
    {
        public static string Current { get; set; } = "";
    }
}
