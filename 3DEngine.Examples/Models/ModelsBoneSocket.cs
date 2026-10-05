// raylib's models_bone_socket example, Copyright (c) 2024-2025 iP (@ipzaur), under the zlib license,
// written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsBoneSocket
{
    private const int BONE_SOCKETS = 3;
    private const int BONE_SOCKET_HAT = 0;
    private const int BONE_SOCKET_HAND_R = 1;
    private const int BONE_SOCKET_HAND_L = 2;
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] bone socket");

        Camera3D camera = new(new Vector3(5.0f, 5.0f, 5.0f), new Vector3(0.0f, 2.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model characterModel = LoadModel("resources/models/gltf/greenman.glb");

        // Each piece of equipment at its socket's index
        Model[] equipModel =
        [
            LoadModel("resources/models/gltf/greenman_hat.glb"),
            LoadModel("resources/models/gltf/greenman_sword.glb"),
            LoadModel("resources/models/gltf/greenman_shield.glb"),
        ];

        bool[] showEquip = [true, true, true];

        int animIndex = 0;
        int animCurrentFrame = 0;
        ModelAnimation[] modelAnimations = LoadModelAnimations("resources/models/gltf/greenman.glb");
        int animsCount = modelAnimations.Length;

        // The bones the equipment hangs from, found by name. raylib's skeleton.bones are Bones here.
        int[] boneSocketIndex = [-1, -1, -1];

        for (int i = 0; i < characterModel.Bones.Length; i++)
        {
            switch (characterModel.Bones[i].Name)
            {
                case "socket_hat": boneSocketIndex[BONE_SOCKET_HAT] = i; break;
                case "socket_hand_R": boneSocketIndex[BONE_SOCKET_HAND_R] = i; break;
                case "socket_hand_L": boneSocketIndex[BONE_SOCKET_HAND_L] = i; break;
            }
        }

        Vector3 position = Vector3.Zero;
        int angle = 0;      // The character's turn in degrees

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.ThirdPerson);

            // F and H turn the character,
            if (IsKeyDown(Key.F)) angle = (angle + 1)%360;
            else if (IsKeyDown(Key.H)) angle = (360 + angle - 1)%360;

            // T and G pick the clip,
            if (IsKeyPressed(Key.T)) animIndex = (animIndex + 1)%animsCount;
            else if (IsKeyPressed(Key.G)) animIndex = (animIndex + animsCount - 1)%animsCount;

            // and 1, 2 and 3 show and hide the hat, the sword and the shield.
            if (IsKeyPressed(Key.Alpha1)) showEquip[BONE_SOCKET_HAT] = !showEquip[BONE_SOCKET_HAT];
            if (IsKeyPressed(Key.Alpha2)) showEquip[BONE_SOCKET_HAND_R] = !showEquip[BONE_SOCKET_HAND_R];
            if (IsKeyPressed(Key.Alpha3)) showEquip[BONE_SOCKET_HAND_L] = !showEquip[BONE_SOCKET_HAND_L];

            ModelAnimation anim = modelAnimations[animIndex];
            animCurrentFrame = (animCurrentFrame + 1)%anim.FrameCount;
            UpdateModelAnimation(characterModel, anim, animCurrentFrame);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // raymath's QuaternionToMatrix, MatrixTranslate and MatrixMultiply are C#'s own.
                    Quaternion characterRotate = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle*DEG2RAD);
                    characterModel.Transform = Matrix4x4.CreateFromQuaternion(characterRotate)*Matrix4x4.CreateTranslation(position);
                    UpdateModelAnimation(characterModel, anim, animCurrentFrame);

                    // raylib draws its file's material 1, its 0 being the default it adds, and here
                    // the mesh's material is the one the file gives it.
                    DrawMesh(characterModel.Meshes[0], characterModel.Materials[characterModel.MeshMaterial[0]], characterModel.Transform);

                    // The equipment, each at its socket
                    for (int i = 0; i < BONE_SOCKETS; i++)
                    {
                        if (!showEquip[i]) continue;

                        Transform transform = anim.FramePoses[animCurrentFrame][boneSocketIndex[i]];
                        Quaternion inRotation = characterModel.BindPose[boneSocketIndex[i]].Rotation;
                        Quaternion outRotation = transform.Rotation;

                        // The socket's turn from its pose at rest to its pose in this frame,
                        Quaternion rotate = outRotation*Quaternion.Inverse(inRotation);
                        Matrix4x4 matrixTransform = Matrix4x4.CreateFromQuaternion(rotate);

                        // moved to where the socket is in this frame,
                        matrixTransform = matrixTransform*Matrix4x4.CreateTranslation(transform.Position);

                        // and carried with the character.
                        matrixTransform = matrixTransform*characterModel.Transform;

                        DrawMesh(equipModel[i].Meshes[0], equipModel[i].Materials[equipModel[i].MeshMaterial[0]], matrixTransform);
                    }

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Use the T/G to switch animation", 10, 10, 20, Color.Gray);
                DrawText("Use the F/H to rotate character left/right", 10, 35, 20, Color.Gray);
                DrawText("Use the 1,2,3 to toggle shown of hat, sword and shield", 10, 60, 20, Color.Gray);

            EndDrawing();
        }

        UnloadModelAnimations(modelAnimations);
        UnloadModel(characterModel);

        for (int i = 0; i < BONE_SOCKETS; i++) UnloadModel(equipModel[i]);

        CloseWindow();
    }
}
