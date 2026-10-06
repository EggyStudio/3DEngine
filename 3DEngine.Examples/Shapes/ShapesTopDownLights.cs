// raylib's shapes_top_down_lights example, Copyright (c) 2022-2025 Jeffery Myers (@JeffM2501), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesTopDownLights
{
    // Custom Blend Modes
    private const RlBlendFactor RLGL_SRC_ALPHA = RlBlendFactor.SrcAlpha;
    private const RlBlendEquation RLGL_MIN = RlBlendEquation.Min;
    private const RlBlendEquation RLGL_MAX = RlBlendEquation.Max;

    private const int MAX_BOXES = 20;
    private const int MAX_SHADOWS = MAX_BOXES*3;     // MAX_BOXES*3 - Each box can cast up to two shadow volumes for the edges it is away from, and one for the box itself
    private const int MAX_LIGHTS = 16;

    // Shadow geometry type
    private struct ShadowGeometry
    {
        public Vector2[] vertices;
    }

    // Light info type
    private struct LightInfo
    {
        public bool active;                // Is this light slot active?
        public bool dirty;                 // Does this light need to be updated?
        public bool valid;                 // Is this light in a valid position?

        public Vector2 position;           // Light position
        public RenderTexture2D mask;       // Alpha mask for the light
        public float outerRadius;          // The distance the light touches
        public Rectangle bounds;           // A cached rectangle of the light bounds to help with culling

        public ShadowGeometry[] shadows;
        public int shadowCount;
    }

    private static readonly LightInfo[] lights = new LightInfo[MAX_LIGHTS];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] top down lights");

        for (int i = 0; i < MAX_LIGHTS; i++)
        {
            lights[i] = default;
            lights[i].shadows = new ShadowGeometry[MAX_SHADOWS];
            for (int s = 0; s < MAX_SHADOWS; s++) lights[i].shadows[s].vertices = new Vector2[4];
        }

        // Initialize our 'world' of boxes
        int boxCount = 0;
        Rectangle[] boxes = new Rectangle[MAX_BOXES];
        SetupBoxes(boxes, ref boxCount);

        // Create a checkerboard ground texture
        Image img = GenImageChecked(64, 64, 32, 32, Color.DarkBrown, Color.DarkGray);
        Texture2D backgroundTexture = LoadTextureFromImage(img);
        UnloadImage(img);

        // Create a global light mask to hold all the blended lights
        RenderTexture2D lightMask = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());

        // Setup initial light
        SetupLight(0, 600, 400, 300);
        int nextLight = 1;

        bool showLines = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Drag light 0
            if (IsMouseButtonDown(MouseButton.Left)) MoveLight(0, GetMousePosition().X, GetMousePosition().Y);

            // Make a new light
            if (IsMouseButtonPressed(MouseButton.Right) && (nextLight < MAX_LIGHTS))
            {
                SetupLight(nextLight, GetMousePosition().X, GetMousePosition().Y, 200);
                nextLight++;
            }

            // Toggle debug info
            if (IsKeyPressed(Key.F1)) showLines = !showLines;

            // Update the lights and keep track if any were dirty so we know if we need to update the master light mask
            bool dirtyLights = false;
            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                if (UpdateLight(i, boxes, boxCount)) dirtyLights = true;
            }

            // Update the light mask
            if (dirtyLights)
            {
                // Build up the light mask
                BeginTextureMode(lightMask);

                    ClearBackground(Color.Black);

                    // Force the blend mode to only set the alpha of the destination
                    rlSetBlendFactors(RLGL_SRC_ALPHA, RLGL_SRC_ALPHA, RLGL_MIN);
                    rlSetBlendMode(BlendMode.Custom);

                    // Merge in all the light masks. A render texture is stored the right way up,
                    // so it is drawn with its height as it is, where raylib's is turned.
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        if (lights[i].active) DrawTextureRec(lights[i].mask.Texture, new Rectangle(0, 0, (float)GetScreenWidth(), (float)GetScreenHeight()), Vector2.Zero, Color.White);
                    }

                    rlDrawRenderBatchActive();

                    // Go back to normal blend
                    rlSetBlendMode(BlendMode.Alpha);
                EndTextureMode();
            }

            BeginDrawing();

                ClearBackground(Color.Black);

                // Draw the tile background
                DrawTextureRec(backgroundTexture, new Rectangle(0, 0, (float)GetScreenWidth(), (float)GetScreenHeight()), Vector2.Zero, Color.White);

                // Overlay the shadows from all the lights
                DrawTextureRec(lightMask.Texture, new Rectangle(0, 0, (float)GetScreenWidth(), (float)GetScreenHeight()), Vector2.Zero, ColorAlpha(Color.White, showLines? 0.75f : 1.0f));

                // Draw the lights
                for (int i = 0; i < MAX_LIGHTS; i++)
                {
                    if (lights[i].active) DrawCircle((int)lights[i].position.X, (int)lights[i].position.Y, 10, (i == 0)? Color.Yellow : Color.White);
                }

                if (showLines)
                {
                    for (int s = 0; s < lights[0].shadowCount; s++)
                    {
                        DrawTriangleFan(lights[0].shadows[s].vertices, Color.DarkPurple);
                    }

                    for (int b = 0; b < boxCount; b++)
                    {
                        if (CheckCollisionRecs(boxes[b],lights[0].bounds)) DrawRectangleRec(boxes[b], Color.Purple);

                        DrawRectangleLines((int)boxes[b].X, (int)boxes[b].Y, (int)boxes[b].Width, (int)boxes[b].Height, Color.DarkBlue);
                    }

                    DrawText("(F1) Hide Shadow Volumes", 10, 50, 10, Color.Green);
                }
                else
                {
                    DrawText("(F1) Show Shadow Volumes", 10, 50, 10, Color.Green);
                }

                DrawFPS(screenWidth - 80, 10);
                DrawText("Drag to move light #1", 10, 10, 10, Color.DarkGreen);
                DrawText("Right click to add new light", 10, 30, 10, Color.DarkGreen);

            EndDrawing();
        }

        UnloadTexture(backgroundTexture);
        UnloadRenderTexture(lightMask);
        for (int i = 0; i < MAX_LIGHTS; i++)
        {
            if (lights[i].active) UnloadRenderTexture(lights[i].mask);
        }

        CloseWindow();
    }

    // Move a light and mark it as dirty so that we update it's mask next frame
    private static void MoveLight(int slot, float x, float y)
    {
        lights[slot].dirty = true;
        lights[slot].position.X = x;
        lights[slot].position.Y = y;

        // update the cached bounds
        lights[slot].bounds = lights[slot].bounds with { X = x - lights[slot].outerRadius, Y = y - lights[slot].outerRadius };
    }

    // Compute a shadow volume for the edge
    // It takes the edge and projects it back by the light radius and turns it into a quad
    private static void ComputeShadowVolumeForEdge(int slot, Vector2 sp, Vector2 ep)
    {
        if (lights[slot].shadowCount >= MAX_SHADOWS) return;

        float extension = lights[slot].outerRadius*2;

        Vector2 spVector = Vector2.Normalize(sp - lights[slot].position);
        Vector2 spProjection = sp + spVector*extension;

        Vector2 epVector = Vector2.Normalize(ep - lights[slot].position);
        Vector2 epProjection = ep + epVector*extension;

        lights[slot].shadows[lights[slot].shadowCount].vertices[0] = sp;
        lights[slot].shadows[lights[slot].shadowCount].vertices[1] = ep;
        lights[slot].shadows[lights[slot].shadowCount].vertices[2] = epProjection;
        lights[slot].shadows[lights[slot].shadowCount].vertices[3] = spProjection;

        lights[slot].shadowCount++;
    }

    // Setup a light
    private static void SetupLight(int slot, float x, float y, float radius)
    {
        lights[slot].active = true;
        lights[slot].valid = false;  // The light must prove it is valid
        lights[slot].mask = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());
        lights[slot].outerRadius = radius;

        lights[slot].bounds = lights[slot].bounds with { Width = radius*2, Height = radius*2 };

        MoveLight(slot, x, y);

        // Force the render texture to have something in it
        DrawLightMask(slot);
    }

    // See if a light needs to update it's mask
    private static bool UpdateLight(int slot, Rectangle[] boxes, int count)
    {
        if (!lights[slot].active || !lights[slot].dirty) return false;

        lights[slot].dirty = false;
        lights[slot].shadowCount = 0;
        lights[slot].valid = false;

        for (int i = 0; i < count; i++)
        {
            // Are we in a box? if so we are not valid
            if (CheckCollisionPointRec(lights[slot].position, boxes[i])) return false;

            // If this box is outside our bounds, we can skip it
            if (!CheckCollisionRecs(lights[slot].bounds, boxes[i])) continue;

            // Check the edges that are on the same side we are, and cast shadow volumes out from them

            // Top
            Vector2 sp = new(boxes[i].X, boxes[i].Y);
            Vector2 ep = new(boxes[i].X + boxes[i].Width, boxes[i].Y);

            if (lights[slot].position.Y > ep.Y) ComputeShadowVolumeForEdge(slot, sp, ep);

            // Right
            sp = ep;
            ep.Y += boxes[i].Height;
            if (lights[slot].position.X < ep.X) ComputeShadowVolumeForEdge(slot, sp, ep);

            // Bottom
            sp = ep;
            ep.X -= boxes[i].Width;
            if (lights[slot].position.Y < ep.Y) ComputeShadowVolumeForEdge(slot, sp, ep);

            // Left
            sp = ep;
            ep.Y -= boxes[i].Height;
            if (lights[slot].position.X > ep.X) ComputeShadowVolumeForEdge(slot, sp, ep);

            // The box itself
            lights[slot].shadows[lights[slot].shadowCount].vertices[0] = new Vector2(boxes[i].X, boxes[i].Y);
            lights[slot].shadows[lights[slot].shadowCount].vertices[1] = new Vector2(boxes[i].X, boxes[i].Y + boxes[i].Height);
            lights[slot].shadows[lights[slot].shadowCount].vertices[2] = new Vector2(boxes[i].X + boxes[i].Width, boxes[i].Y + boxes[i].Height);
            lights[slot].shadows[lights[slot].shadowCount].vertices[3] = new Vector2(boxes[i].X + boxes[i].Width, boxes[i].Y);
            lights[slot].shadowCount++;
        }

        lights[slot].valid = true;

        DrawLightMask(slot);

        return true;
    }

    // Draw the light and shadows to the mask for a light
    private static void DrawLightMask(int slot)
    {
        // Use the light mask
        BeginTextureMode(lights[slot].mask);

            ClearBackground(Color.White);

            // Force the blend mode to only set the alpha of the destination
            rlSetBlendFactors(RLGL_SRC_ALPHA, RLGL_SRC_ALPHA, RLGL_MIN);
            rlSetBlendMode(BlendMode.Custom);

            // If we are valid, then draw the light radius to the alpha mask
            if (lights[slot].valid) DrawCircleGradient(lights[slot].position, lights[slot].outerRadius, ColorAlpha(Color.White, 0), Color.White);

            rlDrawRenderBatchActive();

            // Cut out the shadows from the light radius by forcing the alpha to maximum
            rlSetBlendMode(BlendMode.Alpha);
            rlSetBlendFactors(RLGL_SRC_ALPHA, RLGL_SRC_ALPHA, RLGL_MAX);
            rlSetBlendMode(BlendMode.Custom);

            // Draw the shadows to the alpha mask
            for (int i = 0; i < lights[slot].shadowCount; i++)
            {
                DrawTriangleFan(lights[slot].shadows[i].vertices, Color.White);
            }

            rlDrawRenderBatchActive();

            // Go back to normal blend mode
            rlSetBlendMode(BlendMode.Alpha);

        EndTextureMode();
    }

    // Set up some boxes
    private static void SetupBoxes(Rectangle[] boxes, ref int count)
    {
        boxes[0] = new Rectangle(150,80, 40, 40);
        boxes[1] = new Rectangle(1200, 700, 40, 40);
        boxes[2] = new Rectangle(200, 600, 40, 40);
        boxes[3] = new Rectangle(1000, 50, 40, 40);
        boxes[4] = new Rectangle(500, 350, 40, 40);

        for (int i = 5; i < MAX_BOXES; i++)
        {
            boxes[i] = new Rectangle((float)GetRandomValue(0,GetScreenWidth()), (float)GetRandomValue(0,GetScreenHeight()), (float)GetRandomValue(10,100), (float)GetRandomValue(10,100));
        }

        count = MAX_BOXES;
    }
}
