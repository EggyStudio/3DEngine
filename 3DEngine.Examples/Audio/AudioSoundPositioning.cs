// raylib's audio_sound_positioning example, Copyright (c) 2025 Le Juez Victor (@Bigfoot71), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioSoundPositioning
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] sound positioning");

        InitAudioDevice();

        // The engine's own coin.wav, which shares its name and place with raylib's
        Sound sound = LoadSound("resources/coin.wav");

        Camera3D camera = new(new Vector3(0, 5, 5), Vector3.Zero, Vector3.UnitY, 60, CameraProjection.Perspective);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            float th = (float)GetTime();

            Vector3 spherePos = new(5.0f*MathF.Cos(th), 0.0f, 5.0f*MathF.Sin(th));

            SetSoundPosition(camera, sound, spherePos, 1.0f);

            if (!IsSoundPlaying(sound)) PlaySound(sound);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawGrid(10, 2);
                    DrawSphere(spherePos, 0.5f, Color.Red);
                EndMode3D();

            EndDrawing();
        }

        UnloadSound(sound);

        CloseAudioDevice();

        CloseWindow();
    }

    // A sound's volume and pan from where it is around the listener
    private static void SetSoundPosition(Camera3D listener, Sound sound, Vector3 position, float maxDist)
    {
        // The direction and distance from the listener to the sound
        Vector3 direction = position - listener.Position;
        float distance = direction.Length();

        // Fainter with distance, between 0 and 1
        float attenuation = 1.0f/(1.0f + (distance/maxDist));
        attenuation = Math.Clamp(attenuation, 0.0f, 1.0f);

        // The listener's forward and right
        Vector3 normalizedDirection = Vector3.Normalize(direction);
        Vector3 forward = Vector3.Normalize(listener.Target - listener.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, listener.Up));

        // Fainter behind the listener
        float dotProduct = Vector3.Dot(forward, normalizedDirection);
        if (dotProduct < 0.0f) attenuation *= (1.0f + dotProduct*0.5f);

        // Panned toward the side it is on
        float pan = Vector3.Dot(normalizedDirection, right);

        SetSoundVolume(sound, attenuation);
        SetSoundPan(sound, pan);
    }
}
