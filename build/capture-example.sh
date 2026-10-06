#!/usr/bin/env bash
# Captures an example as the README shows it: opened with no window shown, driven where it waits
# for input, run long enough for its scene to settle and its frame rate to be measured, then
# captured and closed. CI captures every example this way, and a new or changed example's capture
# in .github/assets/examples is taken with it too.
#
#   build/capture-example.sh <example> <png|webp> [--offscreen|--hidden]
#
# A .webp path is written through build/webp.sh, at quality 85 for a lit 3D scene and lossless for
# flat color, 2D shapes or text, as the README's gallery stores them.
#
# Frames are capped at 60 a second, so a wait of a number of frames is at least that many
# sixtieths of a second, and longer on a slow device, which a physics scene's fixed steps catch
# up to.
set -euo pipefail

example="$1"
out="$2"
mode="${3:---offscreen}"
cd "$(dirname "$0")/.."

./e3d open "$example" "$mode" --quiet

# Examples that show a start screen or nothing until they are given input.
case "$example" in
  core_input_gamepad)
    # The console pad, which input.axis connects, with a stick pushed, a trigger half down and
    # a face button held through the capture.
    ./e3d command input.axis 0 LeftX 0.6 --quiet
    ./e3d command input.axis 0 RightTrigger 0.5 --quiet
    ./e3d command input.button 0 South 600 --quiet >/dev/null 2>&1 &
    ;;
  ecs_states)
    ./e3d command input.key Enter 2 --quiet
    ;;
  core_drop_files)
    # Three files dropped, as dragging them from the desktop does.
    for file in levels/forest.json textures/bark.png sounds/wind.ogg; do
      ./e3d command input.drop "/home/player/game/$file" --quiet
    done
    ;;
  audio_raw_stream)
    # The pitch dragged up from 440 Hz, so the wave drawn is the one the stream was given.
    ./e3d command input.move 600 200 --quiet
    ./e3d command input.drag Left 10 0 3 --quiet
    ;;
  core_input_gestures)
    # A tap, then swipes right and up with the mouse, each starting with a tap of its own, then a
    # finger held through the capture. A double tap is left out, since two commands do not
    # reliably land within the 0.3 seconds it allows.
    ./e3d command input.touch 0 200 200 2 --quiet
    ./e3d command input.move 100 300 --quiet
    ./e3d command input.drag Left 300 0 8 --quiet
    ./e3d command input.move 450 380 --quiet
    ./e3d command input.drag Left 0 -250 8 --quiet
    ./e3d command input.touch 0 270 250 600 --quiet >/dev/null 2>&1 &
    ;;
  text_input_box)
    ./e3d command input.text e3d.cs --quiet
    ;;
  models_decals)
    # Decals clicked onto the character's face, chest and legs, the pointer left over its chest.
    for at in "400 200" "380 260" "420 150" "400 300" "370 100"; do
      ./e3d command input.click $at --quiet
    done
    ./e3d command input.move 405 230 --quiet
    ;;
  models_procedural_decals)
    # Targets clicked around the torus, the pointer left over its upper left.
    for at in "400 160" "300 220" "500 250" "420 300"; do
      ./e3d command input.click $at --quiet
    done
    ./e3d command input.move 350 180 --quiet
    ;;
  models_mesh_uv_painting)
    # A red stroke across the sphere, then a sky blue one picked from the palette.
    ./e3d command input.move 430 150 --quiet
    ./e3d command input.drag Left 120 80 30 --quiet
    ./e3d command input.click 61 312 --quiet
    ./e3d command input.move 460 260 --quiet
    ./e3d command input.drag Left -100 30 30 --quiet
    ;;
  shapes_rlgl_color_wheel)
    # A color picked near the top of the wheel, where raylib's screenshot picks one.
    ./e3d command input.click 405 104 --quiet
    ;;
  shapes_top_down_lights)
    # The first light dragged near the middle, where raylib's screenshot has it.
    ./e3d command input.click 410 270 --quiet
    ;;
  textures_magnifying_glass)
    # The glass over the red parrot's head, where raylib's screenshot holds it.
    ./e3d command input.move 510 150 --quiet
    ;;
  shaders_rlgl_compute)
    # A brush of 24 drawn across the grid in a loop, which the capture's wait lets grow.
    ./e3d command input.wheel 16 --quiet
    ./e3d command input.move 200 250 --quiet
    ./e3d command input.drag Left 400 0 12 --quiet
    ./e3d command input.drag Left 0 300 12 --quiet
    ./e3d command input.drag Left -400 0 12 --quiet
    ./e3d command input.drag Left 0 -300 12 --quiet
    ;;
esac

# Scenes where something falls or grows get longer before the capture. The skybox's orbit, half
# a radian a second, lines the spheres up behind each other at a quarter turn, and has them side
# by side again from behind at half a turn, about 380 frames in.
frames=150
case "$example" in
  physics_boxes|ecs_physics|ecs_behaviors|models_stress|textures_bunnymark) frames=300 ;;
  models_skybox) frames=380 ;;
  # Three quarters of the way round its orbit, where raylib's screenshot reads the text.
  text_3d_drawing) frames=450 ;;
esac
# A device drawing on the CPU, as CI's does, can take minutes over the slowest examples' frames.
./e3d command frames.wait "$frames" --quiet --timeout 600

case "$out" in
  *.webp) shot="${out%.webp}.png" ;;
  *) shot="$out" ;;
esac
./e3d shot "$shot" --quiet --timeout 120
./e3d stop --quiet
wait

# The examples that draw a lit 3D scene, whose shading a lossy picture keeps in a fraction of the
# bytes. The rest are flat color, 2D shapes or text, kept exact.
if [ "$shot" != "$out" ]; then
  case "$example" in
    ecs_animated_models|ecs_mesh_entities|ecs_physics|models_*|physics_boxes|scenes_level|\
    shaders_auto_exposure|shaders_bloom|shaders_particles|shaders_compute_texture|shaders_mesh_instancing|shaders_model|\
    shaders_postprocessing|shaders_shadowmap|shaders_basic_lighting|shaders_fog_rendering|shaders_normalmap_rendering|\
    shaders_basic_pbr|shaders_shadowmap_rendering|shaders_vertex_displacement|shaders_lights_bloom|shaders_depth_rendering|\
    shaders_raymarching_rendering|shaders_model_shader|shaders_custom_uniform|shaders_cel_shading) kind=lossy ;;
    *) kind=lossless ;;
  esac
  build/webp.sh "$shot" "$out" "$kind"
  rm -f "$shot"
fi
