// raylib's models_decals example, Copyright (c) 2025 JP Mortiboys (@themushroompirates) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsDecals
{
    private const int MAX_DECALS = 256;

    // A growing list of triangles' corners, and their texture coordinates once the decal is cut
    private sealed class MeshBuilder
    {
        public int vertexCount;
        public Vector3[] vertices = [];
        public Vector2[]? uvs;
    }

    // The two builders the decal is cut between, reading from one and writing to the other
    private static readonly MeshBuilder[] meshBuilders = [new(), new()];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[models] decals");

        Camera3D camera = new(new Vector3(5.0f, 5.0f, 5.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 1.6f, 0.0f), 45.0f, CameraProjection.Perspective);

        // Load character model
        Model model = LoadModel("resources/models/obj/character.obj");

        // Apply character skin
        Texture2D modelTexture = LoadTexture("resources/models/obj/character_diffuse.png");
        SetTextureFilter(modelTexture, TextureFilter.Bilinear);
        model.Materials[0].Texture = modelTexture;

        BoundingBox modelBBox = GetMeshBoundingBox(model.Meshes[0]);    // Get mesh bounding box

        camera.Target = Vector3.Lerp(modelBBox.Min, modelBBox.Max, 0.5f);
        camera.Position = modelBBox.Max*1.0f;
        camera.Position.X *= 0.1f;

        float modelSize = MathF.Min(
            MathF.Min(MathF.Abs(modelBBox.Max.X - modelBBox.Min.X), MathF.Abs(modelBBox.Max.Y - modelBBox.Min.Y)),
            MathF.Abs(modelBBox.Max.Z - modelBBox.Min.Z));

        camera.Position = new Vector3(0.0f, modelBBox.Max.Y*1.2f, modelSize*3.0f);

        float decalSize = modelSize*0.25f;
        float decalOffset = 0.01f;

        Model placementCube = LoadModelFromMesh(GenMeshCube(decalSize, decalSize, decalSize));
        placementCube.Materials[0].Color = Color.Lime;

        ModelMaterial decalMaterial = LoadMaterialDefault();
        decalMaterial.Color = Color.Yellow;

        Image decalImage = LoadImage("resources/raylib_logo.png");
        ImageResizeNN(ref decalImage, decalImage.Width/4, decalImage.Height/4);
        Texture2D decalTexture = LoadTextureFromImage(decalImage);
        UnloadImage(decalImage);

        SetTextureFilter(decalTexture, TextureFilter.Bilinear);
        decalMaterial.Texture = decalTexture;
        decalMaterial.Color = Color.RayWhite;

        bool showModel = true;
        Model[] decalModels = new Model[MAX_DECALS];
        int decalCount = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonDown(MouseButton.Right)) UpdateCamera(ref camera, CameraMode.ThirdPerson);

            // Display information about closest hit
            RayCollision collision = new(false, float.MaxValue, Vector3.Zero, Vector3.Zero);

            // Get mouse ray
            Ray ray = GetScreenToWorldRay(GetMousePosition(), camera);

            // Check ray collision against bounding box first, before trying the full ray-mesh test
            RayCollision boxHitInfo = GetRayCollisionBox(ray, modelBBox);

            if ((boxHitInfo.Hit) && (decalCount < MAX_DECALS))
            {
                // Check ray collision against model meshes
                RayCollision meshHitInfo = default;
                for (int m = 0; m < model.Meshes.Length; m++)
                {
                    // The collision is tested with the model's transform, which a model drawn
                    // several times would give each of its transforms in turn.
                    meshHitInfo = GetRayCollisionMesh(ray, model.Meshes[m], model.Transform);
                    if (meshHitInfo.Hit)
                    {
                        // Save the closest hit mesh
                        if (!collision.Hit || (collision.Distance > meshHitInfo.Distance)) collision = meshHitInfo;
                    }
                }

                if (meshHitInfo.Hit) collision = meshHitInfo;
            }

            // Add decal to mesh on hit point
            if (collision.Hit && IsMouseButtonPressed(MouseButton.Left) && (decalCount < MAX_DECALS))
            {
                // Create the transformation to project the decal
                Vector3 origin = collision.Point + collision.Normal*1.0f;
                Matrix4x4 splat = Matrix4x4.CreateLookAt(collision.Point, origin, new Vector3(0.0f, 1.0f, 0.0f));

                // Spin the placement around a bit
                splat = splat*Matrix4x4.CreateRotationZ(float.DegreesToRadians((float)GetRandomValue(-180, 180)));

                ModelMesh decalMesh = GenMeshDecal(model, splat, decalSize, decalOffset);

                if (decalMesh.VertexCount > 0)
                {
                    int decalIndex = decalCount++;
                    decalModels[decalIndex] = LoadModelFromMesh(decalMesh);
                    decalModels[decalIndex].Materials[0] = decalMaterial;
                }
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    // Draw the model at the origin and default scale
                    if (showModel) DrawModel(model, new Vector3(0.0f, 0.0f, 0.0f), 1.0f, Color.White);

                    // Draw the decal models
                    for (int i = 0; i < decalCount; i++) DrawModel(decalModels[i], Vector3.Zero, 1.0f, Color.White);

                    // If we hit the mesh, draw the box for the decal
                    if (collision.Hit)
                    {
                        Vector3 origin = collision.Point + collision.Normal*1.0f;
                        Matrix4x4 splat = Matrix4x4.CreateLookAt(collision.Point, origin, new Vector3(0, 1, 0));
                        Matrix4x4.Invert(splat, out Matrix4x4 inverse);
                        placementCube.Transform = inverse;
                        DrawModel(placementCube, Vector3.Zero, 1.0f, Fade(Color.White, 0.5f));
                    }

                    DrawGrid(10, 10.0f);
                EndMode3D();

                float yPos = 10;
                float x0 = GetScreenWidth() - 300.0f;
                float x1 = x0 + 100;
                float x2 = x1 + 100;

                DrawText("Vertices", (int)x1, (int)yPos, 10, Color.Lime);
                DrawText("Triangles", (int)x2, (int)yPos, 10, Color.Lime);
                yPos += 15;

                int vertexCount = 0;
                int triangleCount = 0;

                for (int i = 0; i < model.Meshes.Length; i++)
                {
                    vertexCount += model.Meshes[i].VertexCount;
                    triangleCount += model.Meshes[i].TriangleCount;
                }

                DrawText("Main model", (int)x0, (int)yPos, 10, Color.Lime);
                DrawText($"{vertexCount}", (int)x1, (int)yPos, 10, Color.Lime);
                DrawText($"{triangleCount}", (int)x2, (int)yPos, 10, Color.Lime);
                yPos += 15;

                for (int i = 0; i < decalCount; i++)
                {
                    if (i == 20)
                    {
                        DrawText("...", (int)x0, (int)yPos, 10, Color.Lime);
                        yPos += 15;
                    }

                    if (i < 20)
                    {
                        DrawText($"Decal #{i + 1}", (int)x0, (int)yPos, 10, Color.Lime);
                        DrawText($"{decalModels[i].Meshes[0].VertexCount}", (int)x1, (int)yPos, 10, Color.Lime);
                        DrawText($"{decalModels[i].Meshes[0].TriangleCount}", (int)x2, (int)yPos, 10, Color.Lime);
                        yPos += 15;
                    }

                    vertexCount += decalModels[i].Meshes[0].VertexCount;
                    triangleCount += decalModels[i].Meshes[0].TriangleCount;
                }

                DrawText("TOTAL", (int)x0, (int)yPos, 10, Color.Lime);
                DrawText($"{vertexCount}", (int)x1, (int)yPos, 10, Color.Lime);
                DrawText($"{triangleCount}", (int)x2, (int)yPos, 10, Color.Lime);
                yPos += 15;

                DrawText("Hold RMB to move camera", 10, 430, 10, Color.Gray);
                DrawText("(c) Character model and texture from kenney.nl", screenWidth - 260, screenHeight - 20, 10, Color.Gray);

                // UI elements
                if (GuiButton(new Rectangle(10, screenHeight - 100.0f, 100, 60), showModel ? "Hide Model" : "Show Model")) showModel = !showModel;

                if (GuiButton(new Rectangle(10 + 110, screenHeight - 100.0f, 100, 60), "Clear Decals"))
                {
                    // Clear decals, unload all decal models
                    for (int i = 0; i < decalCount; i++) UnloadModel(decalModels[i]);
                    decalCount = 0;
                }

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadModel(placementCube);
        UnloadTexture(modelTexture);

        // Unload decal models
        for (int i = 0; i < decalCount; i++) UnloadModel(decalModels[i]);

        UnloadTexture(decalTexture);

        CloseWindow();
    }

    // Add triangles to mesh builder (dynamic array manager)
    private static void AddTriangleToMeshBuilder(MeshBuilder mb, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        // Grow the array, 256 vertices at a time, when it is full
        if (mb.vertices.Length <= (mb.vertexCount + 3))
            Array.Resize(ref mb.vertices, (1 + (mb.vertices.Length/256))*256);

        // Add 3 vertices
        mb.vertices[mb.vertexCount++] = v0;
        mb.vertices[mb.vertexCount++] = v1;
        mb.vertices[mb.vertexCount++] = v2;
    }

    // Build a mesh from MeshBuilder data. raylib's has no normals, which its unlit default shader
    // does not read, and a model here is lit, so each triangle takes its face's normal.
    private static ModelMesh BuildMesh(MeshBuilder mb)
    {
        ModelVertex[] vertices = new ModelVertex[mb.vertexCount];
        uint[] indices = new uint[mb.vertexCount];

        for (int i = 0; i < mb.vertexCount; i += 3)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(mb.vertices[i + 1] - mb.vertices[i], mb.vertices[i + 2] - mb.vertices[i]));
            if (!float.IsFinite(normal.X)) normal = Vector3.UnitY;

            for (int k = i; k < i + 3; k++)
            {
                vertices[k] = new ModelVertex(mb.vertices[k], normal, mb.uvs is { } uvs ? uvs[k] : Vector2.Zero);
                indices[k] = (uint)k;
            }
        }

        return UploadMesh(vertices, indices);
    }

    // Clip segment
    private static Vector3 ClipSegment(Vector3 v0, Vector3 v1, Vector3 p, float s)
    {
        float d0 = Vector3.Dot(v0, p) - s;
        float d1 = Vector3.Dot(v1, p) - s;
        float s0 = d0/(d0 - d1);

        Vector3 position = Vector3.Lerp(v0, v1, s0);

        return position;
    }

    // Generate mesh decals for provided model
    private static ModelMesh GenMeshDecal(Model target, Matrix4x4 projection, float decalSize, float decalOffset)
    {
        // We're going to need the inverse matrix
        Matrix4x4.Invert(projection, out Matrix4x4 invProj);

        // Reset the mesh builders
        meshBuilders[0].vertexCount = 0;
        meshBuilders[1].vertexCount = 0;

        // We'll be flip-flopping between the two mesh builders
        // Reading from one and writing to the other, then swapping
        int mbIndex = 0;

        // First pass, any triangle inside the bounding box (for each mesh of the model)
        for (int meshIndex = 0; meshIndex < target.Meshes.Length; meshIndex++)
        {
            // The mesh's triangles, three corners each, as raylib reads a mesh without indices
            Vector3[] corners = GetMeshComponent(target.Meshes[meshIndex]).Positions;
            for (int tri = 0; tri < corners.Length/3; tri++)
            {
                Span<Vector3> vertices = [corners[3*tri], corners[3*tri + 1], corners[3*tri + 2]];

                // Transform all 3 vertices of the triangle
                // and check if they are inside our decal box
                int insideCount = 0;
                for (int i = 0; i < 3; i++)
                {
                    // To projection space
                    Vector3 v = Vector3.Transform(vertices[i], projection);

                    if ((MathF.Abs(v.X) < decalSize) || (MathF.Abs(v.Y) <= decalSize) || (MathF.Abs(v.Z) <= decalSize)) insideCount++;

                    // We need to keep the transformed vertex
                    vertices[i] = v;
                }

                // A triangle with any corner inside is added, and clipped later.
                if (insideCount > 0) AddTriangleToMeshBuilder(meshBuilders[mbIndex], vertices[0], vertices[1], vertices[2]);
            }
        }

        // Clipping time! We need to clip against all 6 directions
        Vector3[] planes =
        [
            new(1, 0, 0),
            new(-1, 0, 0),
            new(0, 1, 0),
            new(0, -1, 0),
            new(0, 0, 1),
            new(0, 0, -1),
        ];

        for (int face = 0; face < 6; face++)
        {
            // Swap current model builder, so the one written last is read
            mbIndex = 1 - mbIndex;

            MeshBuilder inMesh = meshBuilders[1 - mbIndex];
            MeshBuilder outMesh = meshBuilders[mbIndex];

            // Reset write builder
            outMesh.vertexCount = 0;

            float s = 0.5f*decalSize;

            for (int i = 0; i < inMesh.vertexCount; i += 3)
            {
                Vector3 nV1 = default, nV2 = default, nV3 = default, nV4 = default;

                float d1 = Vector3.Dot(inMesh.vertices[i + 0], planes[face]) - s;
                float d2 = Vector3.Dot(inMesh.vertices[i + 1], planes[face]) - s;
                float d3 = Vector3.Dot(inMesh.vertices[i + 2], planes[face]) - s;

                bool v1Out = (d1 > 0);
                bool v2Out = (d2 > 0);
                bool v3Out = (d3 > 0);

                // Calculate, how many vertices of the face lie outside of the clipping plane
                int total = (v1Out ? 1 : 0) + (v2Out ? 1 : 0) + (v3Out ? 1 : 0);

                switch (total)
                {
                    case 0:
                    {
                        // The entire face lies inside of the plane, no clipping needed
                        AddTriangleToMeshBuilder(outMesh, inMesh.vertices[i], inMesh.vertices[i + 1], inMesh.vertices[i + 2]);
                    } break;
                    case 1:
                    {
                        // One vertex lies outside of the plane, perform clipping
                        if (v1Out)
                        {
                            nV1 = inMesh.vertices[i + 1];
                            nV2 = inMesh.vertices[i + 2];
                            nV3 = ClipSegment(inMesh.vertices[i], nV1, planes[face], s);
                            nV4 = ClipSegment(inMesh.vertices[i], nV2, planes[face], s);
                        }

                        if (v2Out)
                        {
                            nV1 = inMesh.vertices[i];
                            nV2 = inMesh.vertices[i + 2];
                            nV3 = ClipSegment(inMesh.vertices[i + 1], nV1, planes[face], s);
                            nV4 = ClipSegment(inMesh.vertices[i + 1], nV2, planes[face], s);

                            AddTriangleToMeshBuilder(outMesh, nV3, nV2, nV1);
                            AddTriangleToMeshBuilder(outMesh, nV2, nV3, nV4);
                            break;
                        }

                        if (v3Out)
                        {
                            nV1 = inMesh.vertices[i];
                            nV2 = inMesh.vertices[i + 1];
                            nV3 = ClipSegment(inMesh.vertices[i + 2], nV1, planes[face], s);
                            nV4 = ClipSegment(inMesh.vertices[i + 2], nV2, planes[face], s);
                        }

                        AddTriangleToMeshBuilder(outMesh, nV1, nV2, nV3);
                        AddTriangleToMeshBuilder(outMesh, nV4, nV3, nV2);
                    } break;
                    case 2:
                    {
                        // Two vertices lies outside of the plane, perform clipping
                        if (!v1Out)
                        {
                            nV1 = inMesh.vertices[i];
                            nV2 = ClipSegment(nV1, inMesh.vertices[i + 1], planes[face], s);
                            nV3 = ClipSegment(nV1, inMesh.vertices[i + 2], planes[face], s);
                            AddTriangleToMeshBuilder(outMesh, nV1, nV2, nV3);
                        }

                        if (!v2Out)
                        {
                            nV1 = inMesh.vertices[i + 1];
                            nV2 = ClipSegment(nV1, inMesh.vertices[i + 2], planes[face], s);
                            nV3 = ClipSegment(nV1, inMesh.vertices[i], planes[face], s);
                            AddTriangleToMeshBuilder(outMesh, nV1, nV2, nV3);
                        }

                        if (!v3Out)
                        {
                            nV1 = inMesh.vertices[i + 2];
                            nV2 = ClipSegment(nV1, inMesh.vertices[i], planes[face], s);
                            nV3 = ClipSegment(nV1, inMesh.vertices[i + 1], planes[face], s);
                            AddTriangleToMeshBuilder(outMesh, nV1, nV2, nV3);
                        }
                    } break;
                    default: break; // The entire face lies outside of the plane, so its vertices are discarded
                }
            }
        }

        // Now the vertices are transformed back
        MeshBuilder theMesh = meshBuilders[mbIndex];

        if (theMesh.vertexCount > 0)
        {
            // Room for UVs
            theMesh.uvs = new Vector2[theMesh.vertexCount];

            for (int i = 0; i < theMesh.vertexCount; i++)
            {
                // Calculate the UVs based on the projected coords
                // They are clipped to (-decalSize .. decalSize) and taken to (0..1)
                theMesh.uvs[i].X = (theMesh.vertices[i].X/decalSize + 0.5f);
                theMesh.uvs[i].Y = (theMesh.vertices[i].Y/decalSize + 0.5f);

                // Tiny nudge in the normal direction so it renders properly over the mesh
                theMesh.vertices[i].Z -= decalOffset;

                // From projection space to world space
                theMesh.vertices[i] = Vector3.Transform(theMesh.vertices[i], invProj);
            }

            // Decal model data ready, create the mesh and return it
            return BuildMesh(theMesh);
        }
        else
        {
            // Return a blank mesh as there's nothing to add
            return default;
        }
    }

    // Button UI element
    private static bool GuiButton(Rectangle rec, string label)
    {
        Color bgColor = Color.Gray;
        bool pressed = false;

        if (CheckCollisionPointRec(GetMousePosition(), rec))
        {
            bgColor = Color.LightGray;
            if (IsMouseButtonPressed(MouseButton.Left)) pressed = true;
        }

        DrawRectangleRec(rec, bgColor);
        DrawRectangleLinesEx(rec, 2.0f, Color.DarkGray);

        int fontSize = 10;
        int textWidth = MeasureText(label, fontSize);

        DrawText(label, (int)(rec.X + rec.Width*0.5f - textWidth*0.5f), (int)(rec.Y + rec.Height*0.5f - fontSize*0.5f), fontSize, Color.DarkGray);

        return pressed;
    }
}
