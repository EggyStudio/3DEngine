// raylib's models_mesh_uv_painting example, Copyright (c) 2025 PanicTitan (@PanicTitan), under the
// zlib license, written again for the flat API, with ImGui in raygui's place.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsMeshUvPainting
{
    private const int CANVAS_SIZE = 512;
    private const int PALETTE_COUNT = 8;

    private enum ToolMode { TOOL_PAINT = 0, TOOL_PICKER }
    private enum ShapeType { SHAPE_SPHERE = 0, SHAPE_CUBE, SHAPE_CYLINDER, SHAPE_TORUS }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] mesh uv painting");

        // raygui's default style is light, and ImGui's light one matches its gray buttons.
        ImGui.StyleColorsLight();

        Camera3D camera = new(new Vector3(0.0f, 3.5f, 6.5f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // CPU-side canvas plus the GPU texture it gets pushed to after every stroke
        Image canvasImage = GenImageColor(CANVAS_SIZE, CANVAS_SIZE, Color.RayWhite);
        Texture2D canvasTexture = LoadTextureFromImage(canvasImage);
        SetTextureFilter(canvasTexture, TextureFilter.Bilinear);

        Model model = default!;
        BoundingBox modelBBox = default;
        ShapeType currentShape = ShapeType.SHAPE_SPHERE;
        ChangeShape(ref model, ref modelBBox, ref currentShape, ShapeType.SHAPE_SPHERE, canvasTexture);

        ToolMode currentTool = ToolMode.TOOL_PAINT;
        Color activeColor = Color.Red;
        int brushRadius = 12;
        Vector2 lastHitUV = Vector2.Zero;
        bool hasLastHit = false;

        Color[] palette = [Color.Red, Color.Orange, Color.Gold, Color.Lime, Color.SkyBlue, Color.Purple, Color.DarkGray, Color.White];
        Rectangle uiPanelRec = new(10.0f, 10.0f, 230.0f, 490.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mousePos = GetMousePosition();
            bool isMouseOverUI = CheckCollisionPointRec(mousePos, uiPanelRec);

            if (IsMouseButtonDown(MouseButton.Right)) UpdateCamera(ref camera, CameraMode.ThirdPerson);

            if (!isMouseOverUI && IsMouseButtonDown(MouseButton.Left))
            {
                Ray ray = GetScreenToWorldRay(mousePos, camera);

                if (GetMeshHitUV(ray, model, modelBBox, out Vector2 hitUV))
                {
                    int px = (int)(hitUV.X*CANVAS_SIZE);
                    int py = (int)(hitUV.Y*CANVAS_SIZE);

                    if (currentTool == ToolMode.TOOL_PAINT)
                    {
                        // Stroke from the last hit to this one, guarding against jumps across a UV seam
                        if (hasLastHit && (MathF.Abs(hitUV.X - lastHitUV.X) < 0.25f) && (MathF.Abs(hitUV.Y - lastHitUV.Y) < 0.25f))
                        {
                            int lastPx = (int)(lastHitUV.X*CANVAS_SIZE);
                            int lastPy = (int)(lastHitUV.Y*CANVAS_SIZE);
                            float dist = Vector2.Distance(new Vector2((float)lastPx, (float)lastPy), new Vector2((float)px, (float)py));
                            int steps = (int)(dist/2.0f) + 1;

                            for (int i = 0; i <= steps; i++)
                            {
                                float t = (float)i/(float)steps;
                                ImageDrawCircle(ref canvasImage, (int)float.Lerp((float)lastPx, (float)px, t),
                                    (int)float.Lerp((float)lastPy, (float)py, t), brushRadius, activeColor);
                            }
                        }
                        else ImageDrawCircle(ref canvasImage, px, py, brushRadius, activeColor);

                        UpdateTexture(canvasTexture, canvasImage);
                        lastHitUV = hitUV;
                        hasLastHit = true;
                    }
                    else
                    {
                        activeColor = GetImageColor(canvasImage, px, py);
                        currentTool = ToolMode.TOOL_PAINT;
                        hasLastHit = false;
                    }
                }
                else hasLastHit = false;
            }
            else hasLastHit = false;

            BeginDrawing();

                ClearBackground(new Color(30, 32, 40, 255));

                BeginMode3D(camera);
                    DrawModel(model, Vector3.Zero, 1.0f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                // Side panel
                DrawRectangleRec(uiPanelRec, Fade(Color.Black, 0.8f));
                DrawRectangleLinesEx(uiPanelRec, 2.0f, Color.DarkGray);
                DrawText("MESH UV PAINTER", 25, 22, 20, Color.Gold);

                // raygui's controls, as ImGui's, where raygui places them, at raygui's text size of 10
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
                ImGui.Begin("##panel", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
                ImGui.SetWindowFontScale(10.0f/13.0f);

                // Tool selection toggles
                bool paintActive = (currentTool == ToolMode.TOOL_PAINT);
                bool pickerActive = (currentTool == ToolMode.TOOL_PICKER);
                if (GuiToggle(new Rectangle(25, 55, 95, 32), "PAINT", ref paintActive)) currentTool = ToolMode.TOOL_PAINT;
                if (GuiToggle(new Rectangle(125, 55, 95, 32), "PICKER", ref pickerActive)) currentTool = ToolMode.TOOL_PICKER;

                // Shape selection toggles
                DrawText("Mesh Shape:", 25, 100, 10, Color.LightGray);
                bool sphereActive = (currentShape == ShapeType.SHAPE_SPHERE);
                bool cubeActive = (currentShape == ShapeType.SHAPE_CUBE);
                bool cylinderActive = (currentShape == ShapeType.SHAPE_CYLINDER);
                bool torusActive = (currentShape == ShapeType.SHAPE_TORUS);

                if (GuiToggle(new Rectangle(25, 120, 95, 28), "SPHERE", ref sphereActive))
                    ChangeShape(ref model, ref modelBBox, ref currentShape, ShapeType.SHAPE_SPHERE, canvasTexture);
                if (GuiToggle(new Rectangle(125, 120, 95, 28), "CUBE", ref cubeActive))
                    ChangeShape(ref model, ref modelBBox, ref currentShape, ShapeType.SHAPE_CUBE, canvasTexture);
                if (GuiToggle(new Rectangle(25, 153, 95, 28), "CYLINDER", ref cylinderActive))
                    ChangeShape(ref model, ref modelBBox, ref currentShape, ShapeType.SHAPE_CYLINDER, canvasTexture);
                if (GuiToggle(new Rectangle(125, 153, 95, 28), "TORUS", ref torusActive))
                    ChangeShape(ref model, ref modelBBox, ref currentShape, ShapeType.SHAPE_TORUS, canvasTexture);

                // Color display
                DrawText("Active Color:", 25, 195, 10, Color.LightGray);
                DrawRectangle(125, 193, 95, 20, activeColor);
                DrawRectangleLines(125, 193, 95, 20, Color.White);

                // Color swatches
                DrawText("Palette Swatches:", 25, 225, 10, Color.LightGray);
                for (int i = 0; i < PALETTE_COUNT; i++)
                {
                    Rectangle swatchRec = new(25.0f + (i%4)*48, 245.0f + (i/4)*45, 40.0f, 38.0f);
                    DrawRectangleRec(swatchRec, palette[i]);
                    DrawRectangleLinesEx(swatchRec, 1.0f, Color.White);
                    if (CheckCollisionPointRec(mousePos, swatchRec) && IsMouseButtonPressed(MouseButton.Left)) activeColor = palette[i];
                }

                // Brush size buttons
                DrawText($"Brush Size: {brushRadius}px", 25, 345, 10, Color.LightGray);
                if (GuiButton(new Rectangle(25, 365, 95, 30), "SIZE -") && (brushRadius > 2)) brushRadius -= 2;
                if (GuiButton(new Rectangle(125, 365, 95, 30), "SIZE +") && (brushRadius < 64)) brushRadius += 2;

                // Canvas action buttons
                if (GuiButton(new Rectangle(25, 410, 95, 30), "CLEAR"))
                {
                    ImageClearBackground(ref canvasImage, Color.RayWhite);
                    UpdateTexture(canvasTexture, canvasImage);
                }
                if (GuiButton(new Rectangle(125, 410, 95, 30), "FILL"))
                {
                    ImageClearBackground(ref canvasImage, activeColor);
                    UpdateTexture(canvasTexture, canvasImage);
                }

                ImGui.End();

                DrawText("Left click: paint / pick color   |   Right drag: orbit camera", 260, screenHeight - 25, 10, Color.RayWhite);
                DrawFPS(screenWidth - 90, 15);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadImage(canvasImage);
        UnloadTexture(canvasTexture);

        CloseWindow();
    }

    // raygui's button, as ImGui's at the rectangle
    private static bool GuiButton(Rectangle bounds, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y));
        return ImGui.Button(text, new Vector2(bounds.Width, bounds.Height));
    }

    // raygui's toggle, a button drawn pressed while active, answering whether it was clicked
    private static bool GuiToggle(Rectangle bounds, string text, ref bool active)
    {
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        bool clicked = GuiButton(bounds, text);
        if (active) ImGui.PopStyleColor();
        if (clicked) active = !active;
        return clicked;
    }

    // Unloads the current model and builds a new primitive shape in its place, sharing the
    // same canvas texture, and recomputes the transformed bounding box used for ray testing
    private static void ChangeShape(ref Model model, ref BoundingBox bbox, ref ShapeType currentShape, ShapeType newShape, Texture2D canvasTexture)
    {
        if (model is not null) UnloadModel(model);

        ModelMesh mesh = newShape switch
        {
            ShapeType.SHAPE_SPHERE => GenMeshSphere(1.5f, 32, 32),
            ShapeType.SHAPE_CUBE => GenMeshCube(2.2f, 2.2f, 2.2f),
            ShapeType.SHAPE_CYLINDER => GenMeshCylinder(1.2f, 2.5f, 24),
            ShapeType.SHAPE_TORUS => GenMeshTorus(0.6f, 1.6f, 24, 36),
            _ => GenMeshSphere(1.5f, 32, 32),
        };

        model = LoadModelFromMesh(mesh);

        // GenMeshCylinder builds upward from y = 0, so it is shifted down to center on the origin
        if (newShape == ShapeType.SHAPE_CYLINDER) model.Transform = Matrix4x4.CreateTranslation(0.0f, -1.25f, 0.0f);

        model.Materials[0].Texture = canvasTexture;

        BoundingBox box = GetMeshBoundingBox(model.Meshes[0]);
        box = new BoundingBox(Vector3.Transform(box.Min, model.Transform), Vector3.Transform(box.Max, model.Transform));
        bbox = box;

        currentShape = newShape;
    }

    // Raycasts against the model's mesh triangles and returns the UV coordinate of the
    // closest hit, interpolated from the hit triangle's vertices with barycentric weights
    private static bool GetMeshHitUV(Ray ray, Model model, BoundingBox bbox, out Vector2 outUV)
    {
        outUV = Vector2.Zero;
        if (!GetRayCollisionBox(ray, bbox).Hit) return false; // Fast reject

        // The mesh's triangles, three corners each with their texture coordinates, as raylib
        // reads a mesh without indices
        Mesh mesh = GetMeshComponent(model.Meshes[0]);
        float closestDistance = 1e9f;
        bool found = false;
        Vector2 hitUV = Vector2.Zero;

        for (int tri = 0; tri < mesh.Positions.Length/3; tri++)
        {
            int i0 = 3*tri, i1 = 3*tri + 1, i2 = 3*tri + 2;

            Vector3 a = Vector3.Transform(mesh.Positions[i0], model.Transform);
            Vector3 b = Vector3.Transform(mesh.Positions[i1], model.Transform);
            Vector3 c = Vector3.Transform(mesh.Positions[i2], model.Transform);

            RayCollision hit = GetRayCollisionTriangle(ray, a, b, c);

            if (hit.Hit && (hit.Distance < closestDistance))
            {
                closestDistance = hit.Distance;
                found = true;

                Vector2 uvA = mesh.Uvs![i0];
                Vector2 uvB = mesh.Uvs[i1];
                Vector2 uvC = mesh.Uvs[i2];

                Vector3 w = Vector3Barycenter(hit.Point, a, b, c);

                hitUV.X = w.X*uvA.X + w.Y*uvB.X + w.Z*uvC.X;
                hitUV.Y = w.X*uvA.Y + w.Y*uvB.Y + w.Z*uvC.Y;

                // Wrap into [0, 1] in case of minor floating point drift at UV seams
                hitUV.X -= MathF.Floor(hitUV.X);
                hitUV.Y -= MathF.Floor(hitUV.Y);
            }
        }

        if (found) outUV = hitUV;

        return found;
    }
}
