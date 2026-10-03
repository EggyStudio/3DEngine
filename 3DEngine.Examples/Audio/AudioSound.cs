using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioSound
{
    public static void Run()
    {
        InitWindow(800, 450, "[audio] sound and music");
        InitAudioDevice();

        var coin = LoadSound("resources/coin.wav");
        var drone = LoadMusicStream("resources/drone.ogg");
        PlayMusicStream(drone);

        var volume = 0.5f;
        SetMusicVolume(drone, volume);
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateMusicStream(drone);

            if (IsKeyPressed(Key.Space)) PlaySound(coin);
            if (IsKeyPressed(Key.P))
            {
                if (IsMusicStreamPlaying(drone)) PauseMusicStream(drone);
                else ResumeMusicStream(drone);
            }
            if (IsKeyDown(Key.Up)) volume = Math.Min(1, volume + GetFrameTime());
            if (IsKeyDown(Key.Down)) volume = Math.Max(0, volume - GetFrameTime());
            SetMusicVolume(drone, volume);

            BeginDrawing();
            ClearBackground(Color.RayWhite);
            DrawText("SPACE plays a sound, P pauses the music, UP and DOWN change its volume", 20, 20, 20, Color.DarkGray);
            DrawText($"Music: {(IsMusicStreamPlaying(drone) ? "playing" : "paused")}, {GetMusicTimeLength(drone):0.0} s long, volume {volume:0.00}",
                20, 60, 20, Color.Gray);
            DrawText($"Audio device ready: {IsAudioDeviceReady()}", 20, 100, 20, Color.Gray);

            DrawRectangle(20, 140, 400, 20, Color.LightGray);
            DrawRectangle(20, 140, (int)(400 * volume), 20, Color.Maroon);
            if (IsSoundPlaying(coin)) DrawCircle(460, 150, 12, Color.Gold);
            EndDrawing();
        }

        UnloadSound(coin);
        UnloadMusicStream(drone);
        CloseAudioDevice();
        CloseWindow();
    }
}
