using Engine.Examples;

// Each example is a program of its own, in the manner of raylib's examples, and this file picks
// one by name: dotnet run --project 3DEngine.Examples -- core_3d_camera_free
var examples = new Dictionary<string, Action>
{
    ["core_basic_window"] = CoreBasicWindow.Run,
    ["core_3d_camera_fps"] = Core3DCameraFps.Run,
    ["core_keyboard_testbed"] = CoreKeyboardTestbed.Run,
    ["core_input_actions"] = CoreInputActions.Run,
    ["core_undo_redo"] = CoreUndoRedo.Run,
    ["core_random_sequence"] = CoreRandomSequence.Run,
    ["core_basic_screen_manager"] = CoreBasicScreenManager.Run,
    ["core_storage_values"] = CoreStorageValues.Run,
    ["core_custom_logging"] = CoreCustomLogging.Run,
    ["core_window_letterbox"] = CoreWindowLetterbox.Run,
    ["core_2d_camera_split_screen"] = Core2DCameraSplitScreen.Run,
    ["core_input_multitouch"] = CoreInputMultitouch.Run,
    ["core_delta_time"] = CoreDeltaTime.Run,
    ["core_input_keys"] = CoreInputKeys.Run,
    ["core_input_mouse"] = CoreInputMouse.Run,
    ["core_input_mouse_wheel"] = CoreInputMouseWheel.Run,
    ["core_3d_camera_mode"] = Core3DCameraMode.Run,
    ["core_3d_camera_split_screen"] = Core3DCameraSplitScreen.Run,
    ["core_3d_picking"] = Core3DPicking.Run,
    ["core_world_screen"] = CoreWorldScreen.Run,
    ["core_window_should_close"] = CoreWindowShouldClose.Run,
    ["core_random_values"] = CoreRandomValues.Run,
    ["core_scissor_test"] = CoreScissorTest.Run,
    ["core_smooth_pixelperfect"] = CoreSmoothPixelperfect.Run,
    ["core_render_texture"] = CoreRenderTexture.Run,
    ["core_text_file_loading"] = CoreTextFileLoading.Run,
    ["core_2d_camera"] = Core2DCamera.Run,
    ["core_drop_files"] = CoreDropFiles.Run,
    ["audio_raw_stream"] = AudioRawStream.Run,
    ["core_3d_camera_free"] = Core3DCameraFree.Run,
    ["core_3d_camera_first_person"] = Core3DCameraFirstPerson.Run,
    ["core_input_gamepad"] = CoreInputGamepad.Run,
    ["core_window_flags"] = CoreWindowFlags.Run,
    ["shapes_basic_2d"] = ShapesBasic2D.Run,
    ["shapes_basic_3d"] = ShapesBasic3D.Run,
    ["textures_basic"] = TexturesBasic.Run,
    ["textures_render_target"] = TexturesRenderTarget.Run,
    ["textures_image_drawing"] = TexturesImageDrawing.Run,
    ["textures_mipmaps"] = TexturesMipmaps.Run,
    ["textures_bunnymark"] = TexturesBunnymark.Run,
    ["physics_boxes"] = PhysicsBoxes.Run,
    ["models_animation"] = ModelsAnimation.Run,
    ["models_loading"] = ModelsLoading.Run,
    ["models_mesh_generation"] = ModelsMeshGeneration.Run,
    ["models_terrain"] = ModelsTerrain.Run,
    ["models_skybox"] = ModelsSkybox.Run,
    ["models_reflection_probe"] = ModelsReflectionProbe.Run,
    ["models_morph_and_layers"] = ModelsMorphAndLayers.Run,
    ["models_stress"] = ModelsStress.Run,
    ["shaders_postprocessing"] = ShadersPostprocessing.Run,
    ["shaders_model"] = ShadersModel.Run,
    ["shaders_compute_life"] = ShadersComputeLife.Run,
    ["shaders_compute_texture"] = ShadersComputeTexture.Run,
    ["shaders_bloom"] = ShadersBloom.Run,
    ["shaders_auto_exposure"] = ShadersAutoExposure.Run,
    ["shaders_particles"] = ShadersParticles.Run,
    ["shaders_shadowmap"] = ShadersShadowmap.Run,
    ["shaders_mesh_instancing"] = ShadersMeshInstancing.Run,
    ["core_input_gestures"] = CoreInputGestures.Run,
    ["text_fonts"] = TextFonts.Run,
    ["text_input_box"] = TextInputBox.Run,
    ["text_font_sdf"] = TextFontSdf.Run,
    ["audio_sound"] = AudioSound.Run,
    ["gui_imgui_window"] = GuiImGuiWindow.Run,
    ["ecs_behaviors"] = EcsBehaviors.Run,
    ["ecs_mesh_entities"] = EcsMeshEntities.Run,
    ["ecs_animated_models"] = EcsAnimatedModels.Run,
    ["ecs_states"] = EcsStates.Run,
    ["ecs_physics"] = EcsPhysics.Run,
    ["scenes_level"] = ScenesLevel.Run,
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
