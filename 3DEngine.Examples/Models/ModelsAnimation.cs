using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsAnimation
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] animation");

        var camera = new Camera3D(new Vector3(2.5f, 2, 3.5f), new Vector3(0, 1, 0), Vector3.UnitY, 45);

        // A skinned arm and its clip from the same glTF. build/make-arm-gltf.py writes it.
        var arm = LoadModel("resources/arm.gltf");
        var animations = LoadModelAnimations("resources/arm.gltf");
        var bend = animations[0];

        var frame = 0;
        var playing = true;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);
            if (IsKeyPressed(Key.Space)) playing = !playing;
            if (IsKeyPressed(Key.Right)) frame++;
            if (IsKeyPressed(Key.Left)) frame--;

            // Back and forth, up the clip and then down it again.
            if (playing) frame++;
            var length = bend.FrameCount - 1;
            var shown = Math.Abs(((frame % (2 * length)) + 2 * length) % (2 * length) - length);
            UpdateModelAnimation(arm, bend, length - shown);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawModel(arm, Vector3.Zero, 1, new Color(230, 160, 60));
            // Each bone as a point, joined to its parent.
            var pose = bend.FramePoses[length - shown];
            for (int b = 0; b < bend.BoneCount; b++)
            {
                DrawSphere(pose[b].Position, 0.06f, Color.Red);
                if (bend.Bones[b].Parent >= 0) DrawLine3D(pose[b].Position, pose[bend.Bones[b].Parent].Position, Color.Red);
            }
            DrawGrid(10, 1);
            EndMode3D();

            DrawText($"\"{bend.Name}\": frame {length - shown} of {length}. Space pauses, arrows step.", 10, 10, 20, Color.DarkGray);
            DrawFPS(10, 40);
            EndDrawing();
        }

        UnloadModelAnimations(animations);
        UnloadModel(arm);
        CloseWindow();
    }
}
