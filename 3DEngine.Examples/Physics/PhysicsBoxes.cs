using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class PhysicsBoxes
{
    public static void Run()
    {
        InitWindow(800, 450, "[physics] boxes");

        var camera = new Camera3D(new Vector3(8, 7, 12), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var floor = LoadModelFromMesh(GenMeshCube(16, 1, 16));
        var cone = LoadModelFromMesh(GenMeshCone(0.6f, 1.2f, 12));

        // A floor that never moves, and boxes that fall onto it.
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(16, 1, 16));
        var boxes = new List<(PhysicsBody Body, Color Color)>();
        void Drop(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var at = new Vector3(Random.Shared.NextSingle() * 4 - 2, 3 + boxes.Count * 0.6f % 9, Random.Shared.NextSingle() * 4 - 2);
                boxes.Add((CreatePhysicsBox(at, Vector3.One), new Color(230, (byte)(100 + Random.Shared.Next(110)), 60)));
            }
        }
        Drop(20);

        // Cones shaped by their hull, which tip over and roll as the model does, standing on their
        // base where the cone's origin is.
        var cones = new List<PhysicsBody>();
        void DropCones(int count)
        {
            for (int i = 0; i < count; i++)
                cones.Add(CreatePhysicsConvexHull(cone, new Vector3(Random.Shared.NextSingle() * 6 - 3, 4 + i, Random.Shared.NextSingle() * 6 - 3), mass: 0.5f));
        }
        DropCones(4);

        SetTargetFPS(60);
        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);
            if (IsKeyPressed(Key.Space)) Drop(10);
            if (IsKeyPressed(Key.C)) DropCones(5);

            // A click pushes the box under the pointer away from the camera and up.
            if (IsMouseButtonPressed(MouseButton.Left) &&
                GetRayCollisionPhysics(GetScreenToWorldRay(GetMousePosition(), camera), 100, out var hit) &&
                hit.Body.Kind == BodyKind.Dynamic)
                ApplyPhysicsImpulse(hit.Body, Vector3.Normalize(hit.Point - camera.Position) * 6 + Vector3.UnitY * 3);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            floor.Transform = Matrix4x4.CreateTranslation(0, -0.5f, 0);
            DrawModel(floor, Vector3.Zero, 1, new Color(170, 190, 150));
            foreach (var (body, color) in boxes)
            {
                cube.Transform = GetPhysicsBodyTransform(body);
                DrawModel(cube, Vector3.Zero, 1, IsPhysicsBodyHit(body) ? Color.White : color);
            }
            foreach (var body in cones)
            {
                cone.Transform = GetPhysicsBodyTransform(body);
                DrawModel(cone, Vector3.Zero, 1, new Color(80, 130, 220));
            }
            EndMode3D();

            DrawText($"{boxes.Count} boxes, {cones.Count} cones. Click one to push it, Space drops boxes, C cones.", 10, 10, 20, Color.DarkGray);
            DrawFPS(10, 40);
            EndDrawing();
        }

        foreach (var (body, _) in boxes) DestroyPhysicsBody(body);
        foreach (var body in cones) DestroyPhysicsBody(body);
        UnloadModel(cone);
        UnloadModel(cube);
        UnloadModel(floor);
        CloseWindow();
    }
}
