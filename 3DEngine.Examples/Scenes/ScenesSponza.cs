using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class ScenesSponza
{
    // Intel's Sponza, its long axis along x with the main door at +x and the lion's head at -x, its
    // open atrium in the middle under the sky and a gallery on each floor along either side. The
    // last three stand where pictures of the scene drawn in another engine were taken, as near as
    // was found by eye, the first floor's gallery at the lion's end looking down the atrium to the
    // door's end, the main door seen from above under the vault inside it, lit by the sky and the
    // light off the walls alone, and the atrium's floor at the lion's end looking up.
    private static readonly (string Name, Camera3D Camera)[] Views =
    [
        ("atrium", new Camera3D(new Vector3(9.5f, 1.7f, 0), new Vector3(-8, 5, 0), Vector3.UnitY, 60)),
        ("gallery", new Camera3D(new Vector3(9, 7.6f, -4.5f), new Vector3(-6, 7, -4.5f), Vector3.UnitY, 60)),
        ("lion", new Camera3D(new Vector3(-4, 2, 0), new Vector3(-12, 2.5f, 0), Vector3.UnitY, 60)),
        ("arcades", new Camera3D(new Vector3(0, 1.7f, 2.5f), new Vector3(0, 9, -5), Vector3.UnitY, 60)),
        ("nave", new Camera3D(new Vector3(-9, 1.7f, 0), new Vector3(9, 2, 0), Vector3.UnitY, 60)),
        ("balcony", new Camera3D(new Vector3(-9.71f, 7.46f, 2.43f), new Vector3(-1.68f, 5.96f, -3.33f), Vector3.UnitY, 74)),
        ("door", new Camera3D(new Vector3(12, 3.8f, 0.6f), new Vector3(20.74f, -0.89f, -0.63f), Vector3.UnitY, 72)),
        ("lookup", new Camera3D(new Vector3(-1, 0.5f, 2.5f), new Vector3(-9.01f, 6.38f, 1.37f), Vector3.UnitY, 84)),
    ];

    // What the console or ./e3d asked for, taken by the loop at its next frame.
    private static int? _askedView;
    private static float? _askedHour;
    private static Camera3D? _askedCamera;

    [Command("sponza.look", "Puts the camera at a point looking at another, with a field of view in degrees, 60 unless given: sponza.look <x> <y> <z> <tx> <ty> <tz> [fov]")]
    internal static string Look(float x, float y, float z, float tx, float ty, float tz, float fov = 60)
    {
        _askedCamera = new Camera3D(new Vector3(x, y, z), new Vector3(tx, ty, tz), Vector3.UnitY, fov);
        return "looking";
    }

    [Command("sponza.view", "Moves the camera to a named view of Sponza, by its name or number, or answers the names: sponza.view [name]")]
    internal static string View(string name = "")
    {
        var names = string.Join(", ", Views.Select((v, i) => $"{i + 1} {v.Name}"));
        if (name.Length == 0) return names;
        var index = int.TryParse(name, out var number) ? number - 1 : Array.FindIndex(Views, v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= Views.Length) return $"There is no view {name}. The views are {names}.";
        _askedView = index;
        return $"view {Views[index].Name}";
    }

    [Command("sponza.time", "Sets the hour the sun and the sky stand at, from 0 to 24: sponza.time <hour>")]
    internal static string Time(float hour)
    {
        if (!float.IsFinite(hour) || hour < 0 || hour > 24) return "The hour is from 0 to 24.";
        _askedHour = hour;
        return $"hour {hour:0.##}";
    }

    // Where ./e3d scenes fetch unpacks a scene, the folder E3D_SCENES names or 3DEngine/scenes in
    // the user's cache folder, as .github/E3D.md says.
    private static string ScenesFolder()
    {
        if (Environment.GetEnvironmentVariable("E3D_SCENES") is { Length: > 0 } named) return named;
        string cache;
        if (OperatingSystem.IsWindows())
            cache = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            cache = OperatingSystem.IsMacOS() ? Path.Combine(home, "Library", "Caches")
                : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".cache");
        }
        return Path.Combine(cache, "3DEngine", "scenes");
    }

    public static void Run()
    {
        InitWindow(800, 450, "[scenes] sponza");
        SetTargetFPS(60);

        var file = Path.Combine(ScenesFolder(), "intel-sponza", "sponza.gltf");
        if (!File.Exists(file))
        {
            while (!WindowShouldClose())
            {
                BeginDrawing();
                ClearBackground(new Color(30, 30, 34));
                DrawText("Sponza is not fetched.", 40, 190, 20, Color.RayWhite);
                DrawText("Run ./e3d scenes fetch intel-sponza in the engine's checkout.", 40, 220, 20, Color.RayWhite);
                EndDrawing();
            }
            CloseWindow();
            return;
        }

        // The building is 36 units long, 20 wide and 18 high, so the field's four cascades of cells
        // an eighth of a unit reach 8, 16, 32 and 64 units across, the last holding all of it. Its
        // 3.75 million triangles are built into every cascade the camera's jump to a view moves in
        // one frame, 0.23 to 0.46 s on an RTX 4070, where the default budget took 330 frames, the
        // sky's light reaching the floor in shade through the meshes not yet built meanwhile.
        SetSceneField(4, 0.125f, updateBudget: 4, triangleBudget: 4_000_000);
        SetGlobalIllumination(GlobalIllumination.High);
        // The sun stands at the hour asked, rising over the main door and setting over the lion along
        // the atrium's length, its path leaning 6 degrees, so at noon it lights two thirds of the
        // atrium's floor, 5.6 units wide, past the shadow of its walls and eaves 17.6 high, where 10
        // leaves the floor in it.
        var hour = 12f;
        // The sun, the sky and the camera in physical units at the values of the scene the views
        // are matched against, a sun of 97,050 lux at 5,633 K, a clear sky's 20,000 lux on a level
        // floor, and a camera at ISO 200, 1/200 s and f/16, EV 14.64, under AgX's curve, so the
        // stone the sun lights reads as there and the light in the shade shows how far the bounce
        // falls short of it.
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 6, SunIlluminance = 97050, SkyIlluminance = 20000, SunColor = ColorFromTemperature(5633) });
        SetSkyTime(hour);
        SetShadowDistance(40);
        SetCameraExposure(200, 1 / 200f, 16);
        SetTonemap(Tonemap.AgX);
        var sponza = LoadModel(file);
        // A black box outside the main door, which stands a little apart from its frame, so the
        // sky shows through no gap around it, as the scene the views are matched against holds.
        var blackout = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        blackout.Materials[0].Roughness = 1;

        var view = 0;
        var camera = Views[view].Camera;
        while (!WindowShouldClose())
        {
            for (int i = 0; i < Views.Length; i++)
                if (IsKeyPressed(Key.One + i)) _askedView = i;
            if (_askedView is { } asked)
            {
                (view, camera, _askedView) = (asked, Views[asked].Camera, null);
            }
            if (_askedCamera is { } looked) (camera, _askedCamera) = (looked, null);
            // The arrows move the hour, keys every layout has in the same place, and turn the camera
            // while the right button is held.
            var flying = IsMouseButtonDown(MouseButton.Right);
            if (!flying && IsKeyPressed(Key.Right)) _askedHour = (hour + 1) % 24;
            if (!flying && IsKeyPressed(Key.Left)) _askedHour = (hour + 23) % 24;
            if (_askedHour is { } at)
            {
                (hour, _askedHour) = (at, null);
                SetSkyTime(hour);
            }
            if (flying) UpdateCamera(ref camera, CameraMode.Free);

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(sponza, Vector3.Zero, 1, Color.White);
            DrawModelEx(blackout, new Vector3(20.4f, 1.4f, 0.05f), Vector3.UnitY, 0, new Vector3(0.8f, 4.2f, 4), Color.Black);
            EndMode3D();

            DrawText($"{view + 1}: {Views[view].Name}, {(int)hour:00}:{(int)(hour % 1 * 60):00}", 10, 10, 20, Color.RayWhite);
            DrawText("1 to 8 change the view, the left and right arrows the hour, and the right button held looks around with W, A, S and D.", 10, 36, 10, Color.LightGray);
            DrawText("Intel Sponza (2022) by Frank Meinl and Anton Kaplanyan, from Intel's Sample Library, under CC BY 4.0.", 10, 432, 10, Color.LightGray);
            EndDrawing();
        }

        UnloadModel(sponza);
        UnloadModel(blackout);
        CloseWindow();
    }
}
