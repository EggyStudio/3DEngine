using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class ScenesBistro
{
    // The Bistro's exterior, a quarter of Paris about 110 m along x, east, and 115 along z, south,
    // its streets at the height of 0 and its roofs up to 27 m. A street hung with strings of colored
    // bulbs runs from its north-west to the bistro's terrace at its middle, a second street leaves
    // the terrace's corner to the north-east, a lane runs back from that one past the shops, and a
    // round square with a tree in its middle opens to the south-east. The first view looks at the
    // bistro's corner across the street from the west, 1.66 m over the paving, its scooter before
    // the corner's door and a menu board and a lamp to either side, at a field of view of 48.4
    // degrees. The next four stand in the streets and the square at an eye's height and the last
    // above the whole quarter.
    private static readonly (string Name, Camera3D Camera)[] Views =
    [
        ("corner", new Camera3D(new Vector3(-9.846f, 1.888f, 0.973f), new Vector3(0.029f, 2.428f, -0.509f), Vector3.UnitY, 48.4f)),
        ("terrace", new Camera3D(new Vector3(-10, 1.7f, -8), new Vector3(4, 3.5f, 6), Vector3.UnitY, 60)),
        ("street", new Camera3D(new Vector3(16, 1.7f, -40), new Vector3(0, 2.5f, -12), Vector3.UnitY, 60)),
        ("lane", new Camera3D(new Vector3(2, 1.7f, -16), new Vector3(17, 3, -42), Vector3.UnitY, 60)),
        ("square", new Camera3D(new Vector3(26, 1.7f, 20), new Vector3(54, 2.5f, 12), Vector3.UnitY, 60)),
        ("above", new Camera3D(new Vector3(15, 120, 3), new Vector3(15, 0, 2.9f), Vector3.UnitY, 60)),
    ];

    // What the console or ./e3d asked for, taken by the loop at its next frame.
    private static int? _askedView;
    private static float? _askedHour;
    private static Camera3D? _askedCamera;
    private static bool? _askedMetered;

    [Command("bistro.look", "Puts the camera at a point looking at another, with a field of view in degrees, 60 unless given: bistro.look <x> <y> <z> <tx> <ty> <tz> [fov]")]
    internal static string Look(float x, float y, float z, float tx, float ty, float tz, float fov = 60)
    {
        _askedCamera = new Camera3D(new Vector3(x, y, z), new Vector3(tx, ty, tz), Vector3.UnitY, fov);
        return "looking";
    }

    [Command("bistro.view", "Moves the camera to a named view of the Bistro, by its name or number, or answers the names: bistro.view [name]")]
    internal static string View(string name = "")
    {
        var names = string.Join(", ", Views.Select((v, i) => $"{i + 1} {v.Name}"));
        if (name.Length == 0) return names;
        var index = int.TryParse(name, out var number) ? number - 1 : Array.FindIndex(Views, v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= Views.Length) return $"There is no view {name}. The views are {names}.";
        _askedView = index;
        return $"view {Views[index].Name}";
    }

    [Command("bistro.metered", "Has the camera meter each view, between EV -1 and 14 and center-weighted, or holds it at the hour's, ISO 200, 1/50 s and f/16 while the sun is up and ISO 3200, 1/15 s and f/1.4 once it has set: bistro.metered <on|off>")]
    internal static string Metered(string on)
    {
        _askedMetered = on is "on" or "true" or "1";
        return _askedMetered.Value ? "metered" : "held";
    }

    [Command("bistro.time", "Sets the hour the sun and the sky stand at, from 0 to 24: bistro.time <hour>")]
    internal static string Time(float hour)
    {
        if (!float.IsFinite(hour) || hour < 0 || hour > 24) return "The hour is from 0 to 24.";
        _askedHour = hour;
        return $"hour {hour:0.##}";
    }

    public static void Run()
    {
        InitWindow(800, 450, "[scenes] bistro");
        SetTargetFPS(60);

        // The pack's manifest, copied beside the program from the checkout's scenes folder, names
        // where the pack is fetched from and its model, which is looked for where ./e3d scenes
        // fetch puts it (ScenePacks.Folder), so either way finds what the other fetched.
        ScenePacks.TryRead(Path.Combine(AppContext.BaseDirectory, "scenes", "bistro-exterior.json"), out var pack, out var problem);
        if ((pack is null || !ScenePacks.IsFetched(pack)) && !ScenePackScreen.Fetch(pack, problem, "The Bistro", "bistro-exterior"))
        {
            CloseWindow();
            return;
        }
        var file = ScenePacks.ModelOf(pack!);

        // The field's four cascades of cells a quarter of a meter reach 16, 32, 64 and 128 m across,
        // ahead of the camera, and a far cascade of 240 holds the whole street from anywhere in it.
        SetSceneField(4, 0.25f, farCascade: 240);
        SetGlobalIllumination(GlobalIllumination.High);
        // The corners, the cracks and the feet of the walls darken by the circular occluders the
        // window's depth gives, 16 rays a pixel against the points within half a meter taken as
        // circles, two of the field's cells, finer than the cascades hold.
        SetAmbientOcclusion(1, 0.5f);
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders);
        // A clear day's sun of 120,000 lux outside the air at 5,633 K, through the Sky module's
        // Earth air, its aerosols spread over 12 km above a white ground, under AgX's curve. The
        // day's path leans 22.16 degrees and the day starts at 13:42, the sun 56.6 degrees high to
        // the south-west, lighting the corner's north face and leaving its door in shade. The
        // camera is metered between EV -1 and 14, center-weighted and 0.79 stops under a mid gray,
        // faster to light than to dark, and M holds it at the hour's, ISO 200, 1/50 s and f/16
        // while the sun is up, two stops over a sunny day's, under which the sunlit stone reads
        // near white and the light the street gives back shows in its shade, and a street at
        // night's ISO 3200, 1/15 s and f/1.4 once it has set, under which the 2 lux the lamps give
        // the paving read an eighth to a fifth of white.
        var hour = 13.7f;
        static void HoldCamera(float at)
        {
            if (at is >= 6 and <= 18) SetCameraExposure(200, 1 / 50f, 16);
            else SetCameraExposure(3200, 1 / 15f, 1.4f);
        }
        var air = Sky.Atmosphere.Earth with { AerosolExtinction = 1e-6f, AerosolScaleHeight = 12_000, GroundAlbedo = Vector3.One };
        SetSkyDay(new SkyDay
        {
            Minutes = 0, Tilt = 22.16f, Atmosphere = air, SunIlluminance = 120_000, SunByHeight = true, SunColor = ColorFromTemperature(5633),
        });
        SetSkyTime(hour);
        SetShadowDistance(80);
        HoldCamera(hour);
        SetTonemapLookup("resources/agx_powerful.ktx2");
        SetLiftGammaGain(0, 0.225f, 0);
        SetBloom(0.189f, 0.0907f);
        SetBloomKind(BloomKind.Scattered);
        SetBloomScatter(0.7f);
        // The pack's emission is a luminance in candelas a square meter, the lamps' glass at 500,
        // the string lights' bulbs at 200 and the two shop signs at 100, seen through the camera as
        // its sun is, so by day they glow faintly against the sunlit stone and at night they light
        // the street. Global illumination carries each as a light of its own.
        var bistro = LoadModel(file);
        for (int i = 0; i < bistro.Materials.Length; i++)
            if (bistro.Materials[i].Emissive != Color.Black)
                bistro.Materials[i] = bistro.Materials[i] with { EmissiveLuminance = bistro.Materials[i].EmissiveIntensity };

        var view = 0;
        var camera = Views[view].Camera;
        static Vector3 Ahead(Camera3D looking) => (looking.Target - looking.Position) / 2;
        var fieldAhead = Ahead(camera);
        var metered = true;
        _askedMetered = true;
        while (!WindowShouldClose())
        {
            for (int i = 0; i < Views.Length; i++)
                if (IsKeyPressed(Key.One + i)) _askedView = i;
            if (_askedView is { } asked)
            {
                (view, camera, _askedView) = (asked, Views[asked].Camera, null);
                fieldAhead = Ahead(camera);
            }
            if (_askedCamera is { } looked) (camera, fieldAhead, _askedCamera) = (looked, Ahead(looked), null);
            var flying = IsMouseButtonDown(MouseButton.Right);
            if (!flying && IsKeyPressed(Key.Right)) _askedHour = (hour + 1) % 24;
            if (!flying && IsKeyPressed(Key.Left)) _askedHour = (hour + 23) % 24;
            if (!flying && IsKeyPressed(Key.M)) _askedMetered = !metered;
            if (_askedHour is { } at)
            {
                (hour, _askedHour) = (at, null);
                SetSkyTime(hour);
                HoldCamera(hour);
            }
            if (_askedMetered is { } meter)
            {
                (metered, _askedMetered) = (meter, null);
                SetAutoExposure(metered);
                SetAutoExposureEv100(-1, 14);
                SetAutoExposureSpeeds(3, 1);
                SetAutoExposureMetering(ExposureMetering.CenterWeighted, -0.79f);
            }
            if (flying)
            {
                float Held(Key more, Key less) => (IsKeyDown(more) ? 1 : 0) - (IsKeyDown(less) ? 1 : 0);
                var step = 8f * GetFrameTime();
                var turn = GetMouseDelta() * float.RadiansToDegrees(0.003f);
                UpdateCameraPro(ref camera, new Vector3(Held(Key.W, Key.S), Held(Key.D, Key.A), Held(Key.E, Key.Q)) * step,
                    new Vector3(turn.X, turn.Y, 0), 0);
            }
            SetSceneFieldCenter(camera.Position + fieldAhead);

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(bistro, Vector3.Zero, 1, Color.White);
            EndMode3D();

            BeginUi();
            DrawText($"{view + 1}: {Views[view].Name}, {(int)hour:00}:{(int)(hour % 1 * 60):00}, {(metered ? "metered" : "fixed camera")}", 10, 10, 20, Color.RayWhite);
            DrawText("1 to 6 change the view, the left and right arrows the hour, and M holds the camera at the hour's or meters it again.", 10, 36, 10, Color.LightGray);
            DrawText("The right button held looks around with W, A, S and D, E rising and Q sinking, and F1 hides this.", 10, 50, 10, Color.LightGray);
            DrawText("Amazon Lumberyard Bistro (2017) by Amazon Lumberyard, from NVIDIA's ORCA, under CC BY 4.0.", 10, GetScreenHeight() - 18, 10, Color.LightGray);
            EndUi();
            EndDrawing();
        }

        UnloadModel(bistro);
        CloseWindow();
    }
}
