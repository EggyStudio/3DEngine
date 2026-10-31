using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class ScenesSponza
{
    // Intel's Sponza, its long axis along x with the main door at +x and the lion's head at -x, its
    // open atrium in the middle under the sky and a gallery on each floor along either side.
    private static readonly (string Name, Camera3D Camera)[] Views =
    [
        ("atrium", new Camera3D(new Vector3(9.5f, 1.7f, 0), new Vector3(-8, 5, 0), Vector3.UnitY, 60)),
        ("gallery", new Camera3D(new Vector3(9, 7.6f, -4.5f), new Vector3(-6, 7, -4.5f), Vector3.UnitY, 60)),
        ("lion", new Camera3D(new Vector3(-4, 2, 0), new Vector3(-12, 2.5f, 0), Vector3.UnitY, 60)),
        ("arcades", new Camera3D(new Vector3(0, 1.7f, 2.5f), new Vector3(0, 9, -5), Vector3.UnitY, 60)),
        ("door", new Camera3D(new Vector3(-9, 1.7f, 0), new Vector3(9, 2, 0), Vector3.UnitY, 60)),
    ];

    // What the console or ./e3d asked for, taken by the loop at its next frame.
    private static int? _askedView;
    private static float? _askedHour;

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
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 6 });
        SetSkyTime(hour);
        SetShadowDistance(40);
        var sponza = LoadModel(file);

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
            if (IsKeyPressed(Key.Rightbracket)) _askedHour = (hour + 1) % 24;
            if (IsKeyPressed(Key.Leftbracket)) _askedHour = (hour + 23) % 24;
            if (_askedHour is { } at)
            {
                (hour, _askedHour) = (at, null);
                SetSkyTime(hour);
            }
            if (IsMouseButtonDown(MouseButton.Right)) UpdateCamera(ref camera, CameraMode.Free);

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(sponza, Vector3.Zero, 1, Color.White);
            EndMode3D();

            DrawText($"{view + 1}: {Views[view].Name}, {(int)hour:00}:{(int)(hour % 1 * 60):00}", 10, 10, 20, Color.RayWhite);
            DrawText("1 to 5 change the view, [ and ] the hour, and the right button held looks around with W, A, S and D.", 10, 36, 10, Color.LightGray);
            DrawText("Intel Sponza (2022) by Frank Meinl and Anton Kaplanyan, from Intel's Sample Library, under CC BY 4.0.", 10, 432, 10, Color.LightGray);
            EndDrawing();
        }

        UnloadModel(sponza);
        CloseWindow();
    }
}
