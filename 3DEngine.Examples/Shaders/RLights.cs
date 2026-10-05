// raylib's rlights.h, its lights for the lighting shaders, written again in C# for the shaders
// examples that include it. Altered from the original, which is C. Its Light is RLights.Light here,
// since the engine has a Light of its own.
//
// raylib.lights: some useful functions to deal with lights data
//
// LICENSE: zlib/libpng
//
// Copyright (c) 2017-2024 Victor Fisac (@victorfisac) and Ramon Santamaria (@raysan5)
//
// This software is provided "as-is", without any express or implied warranty. In no event
// will the authors be held liable for any damages arising from the use of this software.
//
// Permission is granted to anyone to use this software for any purpose, including commercial
// applications, and to alter it and redistribute it freely, subject to the following restrictions:
//
//   1. The origin of this software must not be misrepresented; you must not claim that you
//   wrote the original software. If you use this software in a product, an acknowledgment
//   in the product documentation would be appreciated but is not required.
//
//   2. Altered source versions must be plainly marked as such, and must not be misrepresented
//   as being the original software.
//
//   3. This notice may not be removed or altered from any source distribution.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class RLights
{
    public const int MAX_LIGHTS = 4;            // Max dynamic lights supported by shader

    public const int LIGHT_DIRECTIONAL = 0;
    public const int LIGHT_POINT = 1;

    public struct Light
    {
        public int type;
        public bool enabled;
        public Vector3 position;
        public Vector3 target;
        public Color color;

        // Shader locations
        public int enabledLoc;
        public int typeLoc;
        public int positionLoc;
        public int targetLoc;
        public int colorLoc;
    }

    private static int lightsCount = 0;         // Current amount of created lights

    // Create a light and get shader locations
    public static Light CreateLight(int type, Vector3 position, Vector3 target, Color color, Shader shader)
    {
        Light light = default;

        if (lightsCount < MAX_LIGHTS)
        {
            light.enabled = true;
            light.type = type;
            light.position = position;
            light.target = target;
            light.color = color;

            // The lighting shader names them so.
            light.enabledLoc = GetShaderLocation(shader, $"lights[{lightsCount}].enabled");
            light.typeLoc = GetShaderLocation(shader, $"lights[{lightsCount}].type");
            light.positionLoc = GetShaderLocation(shader, $"lights[{lightsCount}].position");
            light.targetLoc = GetShaderLocation(shader, $"lights[{lightsCount}].target");
            light.colorLoc = GetShaderLocation(shader, $"lights[{lightsCount}].color");

            UpdateLightValues(shader, light);

            lightsCount++;
        }

        return light;
    }

    // Send light properties to shader
    public static void UpdateLightValues(Shader shader, Light light)
    {
        // Send to shader light enabled state and type
        SetShaderValue(shader, light.enabledLoc, light.enabled ? 1 : 0);
        SetShaderValue(shader, light.typeLoc, light.type);

        // Send to shader light position and target position values
        SetShaderValue(shader, light.positionLoc, light.position);
        SetShaderValue(shader, light.targetLoc, light.target);

        // Send to shader light color values
        SetShaderValue(shader, light.colorLoc, new Vector4(light.color.R/255.0f, light.color.G/255.0f,
            light.color.B/255.0f, light.color.A/255.0f));
    }
}
