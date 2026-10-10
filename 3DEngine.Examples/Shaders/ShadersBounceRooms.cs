using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersBounceRooms
{
    // A room's walls, floor and ceiling around an inside of size, each slab thick, its middle at the
    // floor's height, with the wall facing -x left out where open says, as a corridor's end is.
    private static void Room(Model slab, Vector3 floor, Vector3 size, float thick, Color color, bool openToward = false)
    {
        var (w, h, d) = (size.X, size.Y, size.Z);
        var t = thick;
        Box(slab, floor + new Vector3(0, -t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
        Box(slab, floor + new Vector3(0, h + t / 2, 0), new Vector3(w + 2 * t, t, d + 2 * t), color);
        Box(slab, floor + new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w + 2 * t, h, t), color);
        Box(slab, floor + new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w + 2 * t, h, t), color);
        if (!openToward) Box(slab, floor + new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), color);
        Box(slab, floor + new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), color);
    }

    private static void Box(Model slab, Vector3 middle, Vector3 size, Color color, float turn = 0) =>
        DrawModelEx(slab, middle, Vector3.UnitY, turn, size, color);

    public static void Run()
    {
        InitWindow(800, 450, "[shaders] bounce rooms");

        // The cases the light that bounces finds hardest, a room each, forty units apart so no
        // room's lamps or field reach another, and a camera fixed on each, which 1 to 8 pick: the
        // Cornell box, a room of thin walls with a lamp outside it, a corridor lit from its open
        // end, a room the sun lights through a window, white blocks beside red walls one, three and
        // six units off, a small bright strip in a dark room, a floor seen at a grazing angle, and
        // a room whose lamp L carries and whose wall M moves, and a glowing block on a floor in the
        // dark, which the bounce carries as a light of its own. G steps through the qualities, Tab
        // shows the window of what the bounce holds, its views, its parts and a reference to
        // measure it by, and `./e3d command gi.reference` traces each view's reference, which
        // `gi.compare` reads it by.
        SetSceneField(3, 0.15f, 2);
        var quality = GlobalIllumination.High;
        SetGlobalIllumination(quality);
        var white = new Color(220, 220, 215);
        var red = new Color(200, 30, 30);
        var green = new Color(30, 170, 40);
        var dim = new Color(60, 60, 58);

        // One sun for every room, high and from -x, through the corridor's open end and the window.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.8f, -1, 0.25f)), new Color(255, 245, 230), 1.5f, castsShadows: true);
        CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
        // The thin room's lamp inside, dim, and the bright one outside its right wall.
        CreatePointLight(new Vector3(40, 2.2f, 0), Color.White, 0.6f, range: 8);
        CreatePointLight(new Vector3(43, 1.5f, 0), Color.White, 25, range: 10, castsShadows: true);
        CreatePointLight(new Vector3(240, 2.6f, 0), new Color(255, 236, 210), 14, range: 16);
        var carried = CreatePointLight(new Vector3(278, 2, -1.5f), new Color(255, 230, 200), 8, range: 10, castsShadows: true);
        Vector3[] spots = [new(278, 2, -1.5f), new(280, 2.4f, 1.5f), new(282, 1.2f, -1)];
        var spot = 0;
        var moved = false;

        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        glow.Materials[0].Emissive = new Color(255, 240, 220);
        glow.Materials[0].EmissiveIntensity = 6;
        var strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        strip.Materials[0].Emissive = new Color(255, 230, 190);
        strip.Materials[0].EmissiveIntensity = 30;
        // Lit as the voxel game's glowstone is, four of the field's cells wide.
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;

        (string Name, Camera3D Camera)[] views =
        [
            ("the Cornell box", new Camera3D(new Vector3(0, 2.5f, 8), new Vector3(0, 2.4f, 0), Vector3.UnitY, 45)),
            ("thin walls, a lamp outside", new Camera3D(new Vector3(38.3f, 1.4f, 1.6f), new Vector3(41.8f, 1.1f, -0.4f), Vector3.UnitY, 60)),
            ("a corridor lit from its end", new Camera3D(new Vector3(87.5f, 1.4f, 0), new Vector3(72, 1, 0), Vector3.UnitY, 60)),
            ("the sun through a window", new Camera3D(new Vector3(122.6f, 1.6f, 2.6f), new Vector3(118, 0.8f, -1), Vector3.UnitY, 65)),
            ("red walls 1, 3 and 6 units off", new Camera3D(new Vector3(172, 4, 9), new Vector3(162, 0.6f, -5), Vector3.UnitY, 50)),
            ("a small bright strip", new Camera3D(new Vector3(200, 1.4f, 2.3f), new Vector3(200, 1.3f, -2.5f), Vector3.UnitY, 65)),
            ("a floor at a grazing angle", new Camera3D(new Vector3(234.5f, 0.35f, 5.5f), new Vector3(245, 0.15f, -5), Vector3.UnitY, 60)),
            ("a lamp carried, a wall moved", new Camera3D(new Vector3(282.6f, 1.7f, 2.6f), new Vector3(278.5f, 1, -1.5f), Vector3.UnitY, 65)),
            ("a glowing block on a floor", new Camera3D(new Vector3(320, 2.6f, 3.2f), new Vector3(320, 0, -0.3f), Vector3.UnitY, 60)),
        ];
        var view = 0;
        var window = false;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            for (int i = 0; i < views.Length; i++)
                if (IsKeyPressed(Key.One + i)) view = i;
            if (IsKeyPressed(Key.G))
            {
                quality = (GlobalIllumination)(((int)quality + 1) % 4);
                SetGlobalIllumination(quality);
            }
            if (IsKeyPressed(Key.L))
            {
                spot = (spot + 1) % spots.Length;
                SetLightPosition(carried, spots[spot]);
            }
            if (IsKeyPressed(Key.M)) moved = !moved;
            if (IsKeyPressed(Key.Tab)) window = !window;

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(views[view].Camera);

            // The Cornell box, open at the front, its slabs three of the field's cells thick.
            Box(slab, new Vector3(0, -0.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(slab, new Vector3(0, 5.15f, 0), new Vector3(5.6f, 0.3f, 5.6f), white);
            Box(slab, new Vector3(0, 2.5f, -2.65f), new Vector3(5.6f, 5.6f, 0.3f), white);
            Box(slab, new Vector3(-2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), red);
            Box(slab, new Vector3(2.65f, 2.5f, 0), new Vector3(0.3f, 5.6f, 5.6f), green);
            Box(slab, new Vector3(-0.9f, 1.5f, -0.8f), new Vector3(1.4f, 3, 1.4f), white, 20);
            Box(slab, new Vector3(1, 0.7f, 0.7f), new Vector3(1.4f, 1.4f, 1.4f), white, -18);
            Box(glow, new Vector3(0, 4.97f, 0), new Vector3(1.6f, 0.06f, 1.6f), Color.White);

            // A closed room of walls a tenth of a unit thick, less than the field's cell, its dim
            // lamp inside and a bright one close outside its right wall, whose light stays out.
            Room(slab, new Vector3(40, 0, 0), new Vector3(4, 3, 4), 0.1f, white);
            Box(slab, new Vector3(40.8f, 0.5f, -0.6f), new Vector3(1, 1, 1), white);

            // A corridor sixteen units long, open at its -x end, where the sun comes in.
            Room(slab, new Vector3(80, 0, 0), new Vector3(16, 2.5f, 2), 0.3f, white, openToward: true);

            // A room with a window four units wide in its -x wall, the sun on its floor.
            Box(slab, new Vector3(120, -0.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(slab, new Vector3(120, 3.15f, 0), new Vector3(6.6f, 0.3f, 6.6f), white);
            Box(slab, new Vector3(120, 1.5f, -3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(slab, new Vector3(120, 1.5f, 3.15f), new Vector3(6.6f, 3, 0.3f), white);
            Box(slab, new Vector3(123.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), green);
            Box(slab, new Vector3(116.85f, 0.25f, 0), new Vector3(0.3f, 0.5f, 6), white);
            Box(slab, new Vector3(116.85f, 2.8f, 0), new Vector3(0.3f, 0.4f, 6), white);
            Box(slab, new Vector3(116.85f, 1.55f, -2.5f), new Vector3(0.3f, 2.1f, 1), white);
            Box(slab, new Vector3(116.85f, 1.55f, 2.5f), new Vector3(0.3f, 2.1f, 1), white);

            // Outdoors, white blocks with a red wall on their +x side 1, 3 and 6 units off, the
            // sun on each wall's face toward its block and none on the block's face toward it.
            Box(slab, new Vector3(165, -0.15f, -6), new Vector3(30, 0.3f, 30), white);
            float[] apart = [1, 3, 6];
            for (int i = 0; i < 3; i++)
            {
                var z = -i * 5f;
                Box(slab, new Vector3(160, 0.75f, z), new Vector3(1, 1.5f, 1), white);
                Box(slab, new Vector3(160.5f + apart[i] + 0.15f, 1.5f, z), new Vector3(0.3f, 3, 2.5f), red);
            }

            // A dark room with a thin strip on its back wall that gives off light of its own.
            Room(slab, new Vector3(200, 0, 0), new Vector3(5, 3, 5), 0.3f, white);
            Box(strip, new Vector3(200, 1.5f, -2.45f), new Vector3(2, 0.06f, 0.06f), Color.White);

            // A room twelve units across, a lamp under its ceiling, its floor seen from a little above.
            Room(slab, new Vector3(240, 0, 0), new Vector3(12, 3, 12), 0.3f, white);

            // A room whose lamp L carries between three places and whose inner wall M moves.
            Room(slab, new Vector3(280, 0, 0), new Vector3(6, 3, 6), 0.3f, white);
            Box(slab, new Vector3(277.15f, 1.5f, 0), new Vector3(0.3f, 3, 6), red);
            Box(slab, new Vector3(moved ? 281.5f : 280, 1.25f, -1), new Vector3(0.2f, 2.5f, 2.5f), green);
            DrawSphere(spots[spot], 0.08f, new Color(255, 230, 200));

            // A closed room in the dark, its floor white and its walls and ceiling dim, a glowing
            // block on the floor in its middle with a low wall beside it that throws a shadow, and
            // four more stacked two by two.
            Box(slab, new Vector3(320, -0.15f, 0), new Vector3(8.6f, 0.3f, 8.6f), white);
            Box(slab, new Vector3(320, 3.15f, 0), new Vector3(8.6f, 0.3f, 8.6f), dim);
            Box(slab, new Vector3(320, 1.5f, -4.15f), new Vector3(8.6f, 3, 0.3f), dim);
            Box(slab, new Vector3(320, 1.5f, 4.15f), new Vector3(8.6f, 3, 0.3f), dim);
            Box(slab, new Vector3(315.85f, 1.5f, 0), new Vector3(0.3f, 3, 8), dim);
            Box(slab, new Vector3(324.15f, 1.5f, 0), new Vector3(0.3f, 3, 8), dim);
            Box(lamp, new Vector3(320, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), new Color(230, 190, 110));
            Box(slab, new Vector3(321.2f, 0.45f, 0), new Vector3(0.15f, 0.9f, 1.2f), dim);
            for (int i = 0; i < 4; i++)
                Box(lamp, new Vector3(317.7f + (i & 1) * 0.6f, 0.3f, -2.1f + (i >> 1) * 0.6f), new Vector3(0.6f, 0.6f, 0.6f), new Color(230, 190, 110));
            EndMode3D();

            DrawText($"{view + 1}: {views[view].Name}. Light that bounces: {quality}.", 10, 10, 20, Color.RayWhite);
            DrawText("1 to 9 change the view, G the quality, L carries the lamp, M moves the wall and Tab shows the bounce's window.", 10, 36, 10, Color.LightGray);
            DrawFPS(10, 420);
            if (window) DrawBounceWindow();
            EndDrawing();
        }

        UnloadModel(slab);
        UnloadModel(glow);
        UnloadModel(strip);
        UnloadModel(lamp);
        CloseWindow();
    }
}
