// raylib's shaders_game_of_life example, Copyright (c) 2025 Jordi Santonja (@JordSant), under the
// zlib license, written again for the flat API, with ImGui in raygui's place.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersGameOfLife
{
    // Interaction mode
    private const int MODE_RUN = 0;
    private const int MODE_PAUSE = 1;
    private const int MODE_DRAW = 2;

    // An example preset pattern
    private readonly record struct PresetPattern(string name, Vector2 position);

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] game of life");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        const int menuWidth = 100;
        const int windowWidth = screenWidth - menuWidth;
        const int windowHeight = screenHeight;

        const int worldWidth = 2048;
        const int worldHeight = 2048;

        const int randomTiles = 8;      // Random preset: divide the world to compute random points in each tile

        // The whole world, upright with a positive height, as a target here keeps its rows from the
        // top, where raylib's negative height turns OpenGL's from the bottom
        Rectangle worldRectSource = new(0, 0, (float)worldWidth, (float)worldHeight);
        Rectangle worldRectDest = new(0, 0, (float)worldWidth, (float)worldHeight);
        Rectangle textureOnScreen = new(0, 0, (float)windowWidth, (float)windowHeight);

        PresetPattern[] presetPatterns =
        [
            new("Glider", new(0.5f, 0.5f)), new("R-pentomino", new(0.5f, 0.5f)), new("Acorn", new(0.5f, 0.5f)),
            new("Spaceships", new(0.1f, 0.5f)), new("Still lifes", new(0.5f, 0.5f)), new("Oscillators", new(0.5f, 0.5f)),
            new("Puffer train", new(0.1f, 0.5f)), new("Glider Gun", new(0.2f, 0.2f)), new("Breeder", new(0.1f, 0.5f)),
            new("Random", new(0.5f, 0.5f)),
        ];

        int numberOfPresets = presetPatterns.Length;

        int zoom = 1;
        float offsetX = (worldWidth - windowWidth)/2.0f;    // Centered on window
        float offsetY = (worldHeight - windowHeight)/2.0f;  // Centered on window
        int framesPerStep = 1;
        int frame = 0;

        int preset = -1;            // No button pressed for preset
        int mode = MODE_RUN;        // Starting mode: running
        bool buttonZoomIn = false;  // Button states: false not pressed
        bool buttonZomOut = false;
        bool buttonFaster = false;
        bool buttonSlower = false;

        // raylib's game_of_life.fs, written in Slang
        Shader shdrGameOfLife = LoadShader("resources/shaders/slang/game_of_life.slang");

        // Set shader uniform size of the world
        int resolutionLoc = GetShaderLocation(shdrGameOfLife, "resolution");
        SetShaderValue(shdrGameOfLife, resolutionLoc, new Vector2((float)worldWidth, (float)worldHeight));

        // Define two textures: the current world and the previous world
        RenderTexture2D world1 = LoadRenderTexture(worldWidth, worldHeight);
        RenderTexture2D world2 = LoadRenderTexture(worldWidth, worldHeight);

        // Sampled by the nearest texel, as raylib samples every texture, so a zoomed cell stays square
        SetTextureFilter(world1.Texture, TextureFilter.Point);
        SetTextureFilter(world2.Texture, TextureFilter.Point);

        BeginTextureMode(world2);
            ClearBackground(Color.RayWhite);
        EndTextureMode();

        Image startPattern = LoadImage("resources/game_of_life/r_pentomino.png");
        UpdateTextureRec(world2.Texture, new Rectangle(worldWidth/2.0f, worldHeight/2.0f, (float)startPattern.Width, (float)startPattern.Height), startPattern.Data);
        UnloadImage(startPattern);

        // The two worlds, swapped each step
        RenderTexture2D currentWorld = world2;
        RenderTexture2D previousWorld = world1;

        // Image to be used in DRAW mode, to be changed with mouse input
        Image? imageToDraw = null;

        Vector2 previousMousePosition = Vector2.Zero;
        int firstColor = -1;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            frame++;

            // Change zoom: both by buttons or by mouse wheel
            float mouseWheelMove = GetMouseWheelMove();
            if (buttonZoomIn || (buttonZomOut && (zoom > 1)) || (mouseWheelMove != 0.0f))
            {
                FreeImageToDraw(ref imageToDraw);  // Zoom change: free the image to draw to be recreated again

                float centerX = offsetX + (windowWidth/2.0f)/zoom;
                float centerY = offsetY + (windowHeight/2.0f)/zoom;
                if (buttonZoomIn || (mouseWheelMove > 0.0f)) zoom *= 2;
                if ((buttonZomOut || (mouseWheelMove < 0.0f)) && (zoom > 1)) zoom /= 2;
                offsetX = centerX - (windowWidth/2.0f)/zoom;
                offsetY = centerY - (windowHeight/2.0f)/zoom;
            }

            // Change speed: number of frames per step
            if (buttonFaster && framesPerStep > 1) framesPerStep--;
            if (buttonSlower) framesPerStep++;

            // Mouse management
            if ((mode == MODE_RUN) || (mode == MODE_PAUSE))
            {
                FreeImageToDraw(ref imageToDraw);  // Free the image to draw: no longer needed in these modes

                // Pan with mouse left button
                Vector2 mousePosition = GetMousePosition();
                if (IsMouseButtonDown(MouseButton.Left) && (mousePosition.X < windowWidth))
                {
                    offsetX -= (mousePosition.X - previousMousePosition.X)/zoom;
                    offsetY -= (mousePosition.Y - previousMousePosition.Y)/zoom;
                }
                previousMousePosition = mousePosition;
            }
            else // MODE_DRAW
            {
                float offsetDecimalX = offsetX - MathF.Floor(offsetX);
                float offsetDecimalY = offsetY - MathF.Floor(offsetY);
                int sizeInWorldX = (int)MathF.Ceiling((float)(windowWidth + offsetDecimalX*zoom)/zoom);
                int sizeInWorldY = (int)MathF.Ceiling((float)(windowHeight + offsetDecimalY*zoom)/zoom);
                if (offsetX + sizeInWorldX >= worldWidth) sizeInWorldX = worldWidth - (int)MathF.Floor(offsetX);
                if (offsetY + sizeInWorldY >= worldHeight) sizeInWorldY = worldHeight - (int)MathF.Floor(offsetY);

                // Create image to draw if not created yet
                if (imageToDraw is null)
                {
                    // raylib draws the part of the world on screen into a render texture and reads
                    // it back at once. A render texture here is drawn as the frame ends, so the part
                    // is cut from the world as the last frame left it, which is the same, since the
                    // world does not step while it is drawn on.
                    Image world = LoadImageFromTexture(currentWorld.Texture);
                    imageToDraw = ImageFromImage(world, new Rectangle(MathF.Floor(offsetX), MathF.Floor(offsetY), (float)sizeInWorldX, (float)sizeInWorldY));
                    UnloadImage(world);
                }

                Vector2 mousePosition = GetMousePosition();
                if (IsMouseButtonDown(MouseButton.Left) && (mousePosition.X < windowWidth))
                {
                    Image image = imageToDraw.Value;
                    int mouseX = (int)(mousePosition.X + offsetDecimalX*zoom)/zoom;
                    int mouseY = (int)(mousePosition.Y + offsetDecimalY*zoom)/zoom;
                    if (mouseX >= sizeInWorldX) mouseX = sizeInWorldX - 1;
                    if (mouseY >= sizeInWorldY) mouseY = sizeInWorldY - 1;
                    if (firstColor == -1) firstColor = (GetImageColor(image, mouseX, mouseY).R < 5)? 0 : 1;
                    int prevColor = (GetImageColor(image, mouseX, mouseY).R < 5)? 0 : 1;

                    ImageDrawPixel(ref image, mouseX, mouseY, (firstColor != 0) ? Color.Black : Color.RayWhite);
                    imageToDraw = image;

                    if (prevColor != firstColor) UpdateTextureRec(currentWorld.Texture, new Rectangle(MathF.Floor(offsetX), MathF.Floor(offsetY), (float)sizeInWorldX, (float)sizeInWorldY), image.Data);
                }
                else firstColor = -1;
            }

            // Load selected preset
            if (preset >= 0)
            {
                Image pattern;
                if (preset < numberOfPresets - 1)   // Preset with pattern image to load
                {
                    pattern = preset switch
                    {
                        0 => LoadImage("resources/game_of_life/glider.png"),
                        1 => LoadImage("resources/game_of_life/r_pentomino.png"),
                        2 => LoadImage("resources/game_of_life/acorn.png"),
                        3 => LoadImage("resources/game_of_life/spaceships.png"),
                        4 => LoadImage("resources/game_of_life/still_lifes.png"),
                        5 => LoadImage("resources/game_of_life/oscillators.png"),
                        6 => LoadImage("resources/game_of_life/puffer_train.png"),
                        7 => LoadImage("resources/game_of_life/glider_gun.png"),
                        _ => LoadImage("resources/game_of_life/breeder.png"),
                    };
                    BeginTextureMode(currentWorld);
                        ClearBackground(Color.RayWhite);
                    EndTextureMode();

                    UpdateTextureRec(currentWorld.Texture, new Rectangle(worldWidth*presetPatterns[preset].position.X - pattern.Width/2.0f,
                                                                         worldHeight*presetPatterns[preset].position.Y - pattern.Height/2.0f,
                                                                         (float)pattern.Width, (float)pattern.Height), pattern.Data);
                }
                else    // Last preset: Random values
                {
                    pattern = GenImageColor(worldWidth/randomTiles, worldHeight/randomTiles, Color.RayWhite);
                    for (int i = 0; i < randomTiles; i++)
                    {
                        for (int j = 0; j < randomTiles; j++)
                        {
                            ImageClearBackground(ref pattern, Color.RayWhite);
                            for (int x = 0; x < pattern.Width; x++)
                            {
                                for (int y = 0; y < pattern.Height; y++)
                                {
                                    if (GetRandomValue(0, 100) < 15) ImageDrawPixel(ref pattern, x, y, Color.Black);
                                }
                            }
                            UpdateTextureRec(currentWorld.Texture,
                                             new Rectangle((float)(pattern.Width*i), (float)(pattern.Height*j),
                                                           (float)pattern.Width, (float)pattern.Height), pattern.Data);
                        }
                    }
                }

                UnloadImage(pattern);

                mode = MODE_PAUSE;
                offsetX = worldWidth*presetPatterns[preset].position.X - (float)windowWidth/zoom/2.0f;
                offsetY = worldHeight*presetPatterns[preset].position.Y - (float)windowHeight/zoom/2.0f;
            }

            // Check window draw inside world limits
            if (offsetX < 0) offsetX = 0;
            if (offsetY < 0) offsetY = 0;
            if (offsetX > worldWidth - (float)windowWidth/zoom) offsetX = worldWidth - (float)windowWidth/zoom;
            if (offsetY > worldHeight - (float)windowHeight/zoom) offsetY = worldHeight - (float)windowHeight/zoom;

            // Rectangles for drawing texture portion to screen
            Rectangle textureSourceToScreen = new(offsetX, offsetY, (float)windowWidth/zoom, (float)windowHeight/zoom);

            // Draw to texture
            if ((mode == MODE_RUN) && ((frame%framesPerStep) == 0))
            {
                // Swap worlds
                (currentWorld, previousWorld) = (previousWorld, currentWorld);

                // Draw to texture
                BeginTextureMode(currentWorld);
                    BeginShaderMode(shdrGameOfLife);
                        DrawTexturePro(previousWorld.Texture, worldRectSource, worldRectDest, Vector2.Zero, 0.0f, Color.RayWhite);
                    EndShaderMode();
                EndTextureMode();
            }

            BeginDrawing();

                DrawTexturePro(currentWorld.Texture, textureSourceToScreen, textureOnScreen, Vector2.Zero, 0.0f, Color.White);

                DrawLine(windowWidth, 0, windowWidth, screenHeight, new Color(218, 218, 218, 255));
                DrawRectangle(windowWidth, 0, screenWidth - windowWidth, screenHeight, new Color(232, 232, 232, 255));

                DrawText("Conway's", 704, 4, 20, Color.DarkBlue);
                DrawText(" game of", 704, 19, 20, Color.DarkBlue);
                DrawText("  life", 708, 34, 20, Color.DarkBlue);
                DrawText("in raylib", 757, 42, 6, Color.Black);

                DrawText("Presets", 710, 58, 8, Color.Gray);

                // raygui's controls, as ImGui's, where raygui places them, at raygui's text size of 10
                ImGui.SetNextWindowPos(new Vector2(windowWidth, 0));
                ImGui.SetNextWindowSize(new Vector2(menuWidth, screenHeight));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("##menu", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
                ImGui.PopStyleVar();
                ImGui.SetWindowFontScale(10.0f/13.0f);

                preset = -1;
                for (int i = 0; i < numberOfPresets; i++)
                    if (Button(new Rectangle(710.0f, 70.0f + 18*i, 80.0f, 16.0f), presetPatterns[i].name)) preset = i;

                ToggleGroup(new Rectangle(710, 258, 80, 16), ["Run", "Pause", "Draw"], ref mode);

                buttonZoomIn = Button(new Rectangle(710, 328, 80, 16), "Zoom in");
                buttonZomOut = Button(new Rectangle(710, 346, 80, 16), "Zoom out");

                buttonFaster = Button(new Rectangle(710, 382, 80, 16), "Faster");
                buttonSlower = Button(new Rectangle(710, 400, 80, 16), "Slower");

                ImGui.End();

                DrawText($"Zoom: {zoom}x", 710, 316, 8, Color.Gray);
                DrawText($"Speed: {framesPerStep} frame{((framesPerStep > 1)? "s" : "")}", 710, 370, 8, Color.Gray);

                DrawFPS(712, 426);

            EndDrawing();
        }

        UnloadShader(shdrGameOfLife);
        UnloadRenderTexture(world1);
        UnloadRenderTexture(world2);

        FreeImageToDraw(ref imageToDraw);

        CloseWindow();
    }

    private static void FreeImageToDraw(ref Image? imageToDraw)
    {
        if (imageToDraw is { } image)
        {
            UnloadImage(image);
            imageToDraw = null;
        }
    }

    // raygui's button, as ImGui's, at the rectangle
    private static bool Button(Rectangle bounds, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y));
        return ImGui.Button(text, new Vector2(bounds.Width, bounds.Height));
    }

    // raygui's toggle group, one toggle under another, two pixels apart as raygui spaces them, the
    // active one drawn pressed
    private static void ToggleGroup(Rectangle bounds, string[] texts, ref int active)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            bool on = (i == active);
            if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (Button(bounds with { Y = bounds.Y + (bounds.Height + 2)*i }, texts[i])) active = i;
            if (on) ImGui.PopStyleColor();
        }
    }
}
