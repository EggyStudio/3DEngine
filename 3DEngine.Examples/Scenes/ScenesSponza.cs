using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class ScenesSponza
{
    // Intel's Sponza, its long axis along x with the main door at +x and the lion's head at -x, its
    // open atrium in the middle under the sky and a gallery on each floor along either side. The
    // last three stand about where pictures of the scene drawn in another engine were taken, the
    // first floor's gallery at the lion's end looking down the atrium to the door's end, the main
    // door seen from above under the vault inside it, lit by the sky and the light off the walls
    // alone, and the atrium's floor at the lion's end looking up.
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
    private static bool? _askedMetered;
    private static bool? _askedLamps;
    private static (float Radians, int Frames) _turning;

    [Command("sponza.look", "Puts the camera at a point looking at another, with a field of view in degrees, 60 unless given: sponza.look <x> <y> <z> <tx> <ty> <tz> [fov]")]
    internal static string Look(float x, float y, float z, float tx, float ty, float tz, float fov = 60)
    {
        _askedCamera = new Camera3D(new Vector3(x, y, z), new Vector3(tx, ty, tz), Vector3.UnitY, fov);
        return "looking";
    }

    [Command("sponza.turn", "Turns the camera about its point by an angle in degrees a frame, to the right where above 0, for a count of frames, a pose a frame, as a scripted turn is read by: sponza.turn <degrees> <frames>")]
    internal static string Turn(float degrees, int frames)
    {
        _turning = (float.DegreesToRadians(degrees), Math.Max(0, frames));
        return $"turning {degrees} degrees a frame for {_turning.Frames} frames";
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

    [Command("sponza.metered", "Has the camera meter each view as the camera of the scene the views are matched against does, between EV -1 and 14 and center-weighted, or holds it at its ISO 200, 1/200 s and f/16: sponza.metered <on|off>")]
    internal static string Metered(string on)
    {
        _askedMetered = on is "on" or "true" or "1";
        return _askedMetered.Value ? "metered" : "held";
    }

    [Command("sponza.lamps", "Turns the lamps of the galleries and the vestibule on or off, off when the example starts: sponza.lamps <on|off>")]
    internal static string Lamps(string on)
    {
        _askedLamps = on is "on" or "true" or "1";
        return _askedLamps.Value ? "lamps on" : "lamps off";
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

        // The building is 36 units long, 20 wide and 18 high, and the field's four cascades of
        // cells an eighth of a unit reach 8, 16, 32 and 64 units across, ahead of the camera, so
        // the last holds all of it only from around its middle. From the lion's end the main door
        // lies past the four, where a ray takes the sky and brings the morning sun's down the nave,
        // so a far cascade of 100 holds the whole building from anywhere in it. The default budgets
        // build its 3.75 million triangles over frames, the meshes waiting standing in as nothing
        // where they never moved, so no frame splats every cascade a walk carries on at once.
        SetSceneField(4, 0.125f, farCascade: 100);
        SetGlobalIllumination(GlobalIllumination.High);
        // The corners, the cracks and the feet of the columns darken by the horizons the window's
        // depth gives within half a unit, four of the field's cells, finer than the cascades hold.
        SetAmbientOcclusion(1, 0.5f);
        // The air holds no fog, since the fog lit the air of the dark rooms that the light reaching
        // them leaves dark, and showed the sun as an even haze rather than shafts (RENDERING.md).
        // The sun stands at the hour asked, rising over the main door and setting over the lion along
        // the atrium's length, its path leaning 6 degrees, so at noon it lights two thirds of the
        // atrium's floor, 5.6 units wide, past the shadow of its walls and eaves 17.6 high, where 10
        // leaves the floor in it.
        var hour = 12f;
        // The sun, the sky and the camera in physical units. The sun is set outside the air to
        // 120,000 lux times the sine of its height, about a clear day's sun outside the air, 5,000
        // at least as it nears the horizon, at 5,633 K, and lit through the Sky module's Earth air,
        // its aerosols spread over 12 km above a white ground, so the light dims and warms through
        // the air as the hour moves and the sky is the air's, 97,050 lux outside reaching the
        // ground at 4,924 K with the sun 54 degrees high. The camera is at ISO 200, 1/200 s and
        // f/16, EV 14.64, a sunny day's exposure, under AgX's curve. Held there, the stone the sun
        // lights reads as there and the light in the shade shows how far the indirect light falls
        // short of it, but the shade lies so far under the sunlit stone that played it looks as
        // though it had no indirect light, so the example starts metered and M holds the camera at
        // those values.
        var air = Sky.Atmosphere.Earth with { AerosolExtinction = 1e-6f, AerosolScaleHeight = 12_000, GroundAlbedo = Vector3.One };
        void SetHour(float at)
        {
            // The sun's height on the path the day sky gives it, leaning 6 degrees.
            var height = MathF.Cos(float.DegreesToRadians(6)) * MathF.Sin(float.DegreesToRadians((at - 6) * 15));
            SetSkyDay(new SkyDay
            {
                Minutes = 0, Tilt = 6, Atmosphere = air, SunIlluminance = MathF.Max(5_000, 120_000 * height), SunColor = ColorFromTemperature(5633),
            });
            SetSkyTime(at);
        }
        SetHour(hour);
        SetShadowDistance(40);
        SetCameraExposure(200, 1 / 200f, 16);
        // The frame graded by the AgX lookup at resources/agx_powerful.ktx2, which bends the light
        // past white into the highlights rather than clipping it, with the middle tones the lookup
        // darkens lifted by a gamma of 0.225, and bloomed at an intensity of 0.25, a threshold of
        // 0.333 and a scatter of 0.7, so the sun's disc and a lit lamp's glass take a soft halo past
        // their edges and the stone under the threshold none. The intensity is 2 to that power less
        // 1, the threshold is decoded from sRGB to be measured on linear light, and the scatter is
        // laid over the chain's range from 0.05 to 0.95.
        SetTonemapLookup("resources/agx_powerful.ktx2");
        SetLiftGammaGain(new Vector4(1, 1, 1, 0), new Vector4(1, 1, 1, 0.225f), new Vector4(1, 1, 1, 0));
        SetBloom(MathF.Pow(2, 0.25f) - 1, MathF.Pow((0.333f + 0.055f) / 1.055f, 2.4f));
        SetBloomScatter(0.05f + 0.9f * 0.7f);
        var sponza = LoadModel(file);
        // A black box outside the main door, which stands a little apart from its frame, so the sky
        // shows through no gap around it and the vestibule takes its light from the court alone.
        var blackout = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        blackout.Materials[0].Roughness = 1;
        // The lamps' glass, the pack's one gray material with no texture and no light of its own,
        // four panes about each bulb open at the top and the bottom, which the pack keeps solid, so
        // a lamp's light left only through the openings and lit a spot on the floor under it, as
        // under the vestibule's lamp by the wooden door. Glass lets the light through, so the panes
        // stand in no light's way, left out of the shadow maps and of global illumination's field,
        // glow lights and reference, and are drawn mostly clear.
        for (int i = 0; i < sponza.Materials.Length; i++)
        {
            var m = sponza.Materials[i];
            if (!IsTextureValid(m.Texture) && m.Emissive == Color.Black && m.Color.R == m.Color.G && m.Color.G == m.Color.B && m.Color.R > 0)
                sponza.Materials[i] = m with { Color = m.Color with { A = 40 }, AlphaMode = MaterialAlphaMode.Blend, CastsShadows = false, BouncesLight = false };
        }
        // The bulbs, the pack's one material that glows, a bulb in each of its 23 lamps, are the
        // lamps' light, kept to turn them on with L and dark until then. Each lamp's bulb has a
        // material of its own in a model drawn over the pack's meshes, so each burns at a color of
        // its own, two thirds of them warm, between 2,400 and 3,000 K, and a third cool, between
        // 4,000 and 5,500 K, drawn from a seed so every run draws the same lamps. Global illumination carries
        // each bulb as a light of its own (GlowLights), 300 times the pack's emission, at which the
        // vault above a lamp and the floor under it read lit in the evening as a path-traced
        // reference reads them.
        var glowing = Enumerable.Range(0, sponza.Materials.Length).Where(i => sponza.Materials[i].Emissive != Color.Black).ToHashSet();
        var materials = sponza.Materials.ToList();
        var meshMaterial = sponza.MeshMaterial.ToArray();
        var bulbMeshes = Enumerable.Range(0, meshMaterial.Length).Where(i => glowing.Contains(meshMaterial[i])).ToArray();
        var seeded = new Random(23);
        var cooler = bulbMeshes.OrderBy(_ => seeded.Next()).Take(bulbMeshes.Length / 3).ToHashSet();
        var bulbs = new List<(int Index, Color Emissive)>();
        foreach (var mesh in bulbMeshes)
        {
            var kelvin = cooler.Contains(mesh) ? 4000 + 1500 * seeded.NextSingle() : 2400 + 600 * seeded.NextSingle();
            materials.Add(materials[meshMaterial[mesh]] with { EmissiveIntensity = 300 });
            meshMaterial[mesh] = materials.Count - 1;
            bulbs.Add((materials.Count - 1, ColorFromTemperature(kelvin)));
        }
        var lit = new Model { Meshes = sponza.Meshes, Materials = [.. materials], MeshMaterial = meshMaterial };
        void SetLamps(bool on)
        {
            foreach (var (index, emissive) in bulbs)
                lit.Materials[index] = lit.Materials[index] with { Emissive = on ? emissive : Color.Black };
        }
        var lampsOn = false;
        SetLamps(lampsOn);

        var view = 0;
        var camera = Views[view].Camera;
        // The field lies around a point half the way from the camera to what the view looks at, as
        // each view is chosen, and moves with the camera as it flies but not as it turns, so a turn
        // builds nothing and the third cascade, 32 across, holds the part of the building the view
        // shows, from the main door's vestibule to the lion's end, where around the court's middle
        // the vestibule lay in the fourth's coarse probes and took the sky's light.
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
            // A scripted turn about the camera's point, which moves no cascade of the field.
            if (_turning.Frames > 0)
            {
                camera.Target = camera.Position + Vector3.Transform(camera.Target - camera.Position, Matrix4x4.CreateRotationY(-_turning.Radians));
                _turning.Frames--;
            }
            // The arrows move the hour, keys every layout has in the same place, and turn the
            // camera while the right button is held.
            var flying = IsMouseButtonDown(MouseButton.Right);
            if (!flying && IsKeyPressed(Key.Right)) _askedHour = (hour + 1) % 24;
            if (!flying && IsKeyPressed(Key.Left)) _askedHour = (hour + 23) % 24;
            if (!flying && IsKeyPressed(Key.M)) _askedMetered = !metered;
            if (!flying && IsKeyPressed(Key.L)) _askedLamps = !lampsOn;
            if (_askedLamps is { } lamps)
            {
                (lampsOn, _askedLamps) = (lamps, null);
                SetLamps(lampsOn);
            }
            if (_askedHour is { } at)
            {
                (hour, _askedHour) = (at, null);
                SetHour(hour);
            }
            // Metered between EV -1 and 14 about the camera's EV, center-weighted, its mean brought
            // to 12.5 / 120 by a light meter's calibration of 12.5, 0.79 stops under a mid gray, so
            // the sunlit stone keeps its texture where a view holds both sun and shade, and faster
            // to light than to dark, as an eye adapts.
            if (_askedMetered is { } meter)
            {
                (metered, _askedMetered) = (meter, null);
                SetAutoExposure(metered, MathF.Pow(2, GetCameraEv100() - 14), MathF.Pow(2, GetCameraEv100() + 1));
                SetAutoExposureSpeeds(3, 1);
                SetAutoExposureMetering(ExposureMetering.CenterWeighted, MathF.Log2(12.5f / 120 / 0.18f));
            }
            if (flying)
            {
                // The free camera's flight with its own keys, rising on E and sinking on Q, which
                // the free camera rolls by, and turning with the mouse as it does, 0.003 radians a
                // pixel.
                float Held(Key more, Key less) => (IsKeyDown(more) ? 1 : 0) - (IsKeyDown(less) ? 1 : 0);
                var step = 5.4f * GetFrameTime();
                var turn = GetMouseDelta() * float.RadiansToDegrees(0.003f);
                UpdateCameraPro(ref camera, new Vector3(Held(Key.W, Key.S), Held(Key.D, Key.A), Held(Key.E, Key.Q)) * step,
                    new Vector3(turn.X, turn.Y, 0), 0);
            }
            SetSceneFieldCenter(camera.Position + fieldAhead);

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(lit, Vector3.Zero, 1, Color.White);
            DrawModelEx(blackout, new Vector3(20.4f, 1.4f, 0.05f), Vector3.UnitY, 0, new Vector3(0.8f, 4.2f, 4), Color.Black);
            EndMode3D();

            // The interface, which F1 hides, as for a picture of the building alone.
            BeginUi();
            DrawText($"{view + 1}: {Views[view].Name}, {(int)hour:00}:{(int)(hour % 1 * 60):00}, {(metered ? "metered" : "fixed camera")}, {(lampsOn ? "lamps on" : "lamps off")}", 10, 10, 20, Color.RayWhite);
            DrawText("1 to 8 change the view, the left and right arrows the hour, and M meters the exposure or holds the camera at ISO 200, 1/200 s and f/16.", 10, 36, 10, Color.LightGray);
            DrawText("The right button held looks around with W, A, S and D, E rising and Q sinking, L turns the lamps on and off, and F1 hides this.", 10, 50, 10, Color.LightGray);
            DrawText("Intel Sponza (2022) by Frank Meinl and Anton Kaplanyan, from Intel's Sample Library, under CC BY 4.0.", 10, GetScreenHeight() - 18, 10, Color.LightGray);
            EndUi();
            EndDrawing();
        }

        UnloadModel(sponza);
        UnloadModel(blackout);
        CloseWindow();
    }
}
