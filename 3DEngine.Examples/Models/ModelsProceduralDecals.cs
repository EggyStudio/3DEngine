// raylib's models_procedural_decals example, Copyright (c) 2025 PanicTitan (@PanicTitan), under the
// zlib license, written again for the flat API, with ImGui in raygui's place.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsProceduralDecals
{
    private const int MAX_DECALS = 256;

    // Growable triangle-soup buffer used while building a clipped decal mesh
    private sealed class MeshBuilder
    {
        public int vertexCount;
        public Vector3[] vertices = [];
        public Vector2[]? uvs;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[models] procedural decals");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Camera3D camera = new(new Vector3(0.0f, 2.5f, 5.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Target model: a procedurally generated knot with a checkerboard skin
        Model model = LoadModelFromMesh(GenMeshTorus(0.8f, 1.8f, 32, 64));

        Image checkerImage = GenImageChecked(512, 512, 32, 32, Color.LightGray, Color.Gray);
        Texture2D modelTexture = LoadTextureFromImage(checkerImage);
        UnloadImage(checkerImage);
        SetTextureFilter(modelTexture, TextureFilter.Bilinear);
        model.Materials[0].Texture = modelTexture;

        BoundingBox modelBBox = GetMeshBoundingBox(model.Meshes[0]);
        camera.Target = Vector3.Lerp(modelBBox.Min, modelBBox.Max, 0.5f);

        float modelSize = MathF.Min(MathF.Min(MathF.Abs(modelBBox.Max.X - modelBBox.Min.X),
            MathF.Abs(modelBBox.Max.Y - modelBBox.Min.Y)), MathF.Abs(modelBBox.Max.Z - modelBBox.Min.Z));

        float decalSize = modelSize*0.35f;
        float decalOffset = 0.01f;

        // Translucent cube previewing where the next decal will land
        Model placementCube = LoadModelFromMesh(GenMeshCube(decalSize, decalSize, decalSize));
        placementCube.Materials[0].Color = Color.Lime;

        // Decal texture: a procedurally drawn target/bullseye, no image file needed
        Image decalImage = GenImageColor(128, 128, Color.Blank);
        ImageDrawCircle(ref decalImage, 64, 64, 60, Color.Red);
        ImageDrawCircle(ref decalImage, 64, 64, 45, Color.White);
        ImageDrawCircle(ref decalImage, 64, 64, 30, Color.Red);
        ImageDrawCircle(ref decalImage, 64, 64, 15, Color.Yellow);

        Texture2D decalTexture = LoadTextureFromImage(decalImage);
        UnloadImage(decalImage);
        SetTextureFilter(decalTexture, TextureFilter.Bilinear);

        ModelMaterial decalMaterial = LoadMaterialDefault();
        decalMaterial.Texture = decalTexture;
        decalMaterial.Color = Color.RayWhite;

        bool showModel = true;
        Model[] decals = new Model[MAX_DECALS];
        int decalCount = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonDown(MouseButton.Right)) UpdateCamera(ref camera, CameraMode.ThirdPerson);

            // Cast a ray from the mouse and keep the closest point it hits on the model
            Ray ray = GetScreenToWorldRay(GetMousePosition(), camera);
            RayCollision collision = default;

            if (GetRayCollisionBox(ray, modelBBox).Hit && (decalCount < MAX_DECALS))
            {
                for (int m = 0; m < model.Meshes.Length; m++)
                {
                    RayCollision meshHit = GetRayCollisionMesh(ray, model.Meshes[m], model.Transform);
                    if (meshHit.Hit && (!collision.Hit || (meshHit.Distance < collision.Distance))) collision = meshHit;
                }
            }

            // Project a new decal at the hit point, facing along the surface normal
            if (collision.Hit && IsMouseButtonPressed(MouseButton.Left) && (decalCount < MAX_DECALS))
            {
                Vector3 lookTarget = collision.Point + collision.Normal;
                Matrix4x4 splat = Matrix4x4.CreateLookAt(collision.Point, lookTarget, new Vector3(0.0f, 1.0f, 0.0f));
                splat = splat*Matrix4x4.CreateRotationZ(float.DegreesToRadians((float)GetRandomValue(-180, 180)));

                ModelMesh decalMesh = GenMeshDecal(model, splat, decalSize, decalOffset);

                if (decalMesh.VertexCount > 0)
                {
                    decals[decalCount] = LoadModelFromMesh(decalMesh);
                    decals[decalCount].Materials[0] = decalMaterial;
                    decalCount++;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    if (showModel) DrawModel(model, Vector3.Zero, 1.0f, Color.White);

                    for (int i = 0; i < decalCount; i++) DrawModel(decals[i], Vector3.Zero, 1.0f, Color.White);

                    // Preview cube at the surface point currently under the mouse
                    if (collision.Hit)
                    {
                        Vector3 lookTarget = collision.Point + collision.Normal;
                        Matrix4x4 splat = Matrix4x4.CreateLookAt(collision.Point, lookTarget, new Vector3(0.0f, 1.0f, 0.0f));
                        Matrix4x4.Invert(splat, out Matrix4x4 inverse);
                        placementCube.Transform = inverse;
                        DrawModel(placementCube, Vector3.Zero, 1.0f, Fade(Color.White, 0.5f));
                    }

                    DrawGrid(10, 1.0f);

                EndMode3D();

                // Vertex/triangle counts: base model vs total once decal geometry is added
                int baseVertices = 0, baseTriangles = 0;
                for (int i = 0; i < model.Meshes.Length; i++)
                {
                    baseVertices += model.Meshes[i].VertexCount;
                    baseTriangles += model.Meshes[i].TriangleCount;
                }

                int totalVertices = baseVertices, totalTriangles = baseTriangles;
                for (int i = 0; i < decalCount; i++)
                {
                    totalVertices += decals[i].Meshes[0].VertexCount;
                    totalTriangles += decals[i].Meshes[0].TriangleCount;
                }

                int statX = screenWidth - 280;
                DrawText("Vertices", statX + 90, 10, 10, Color.Lime);
                DrawText("Triangles", statX + 180, 10, 10, Color.Lime);
                DrawText("Base Model", statX, 25, 10, Color.Lime);
                DrawText($"{baseVertices}", statX + 90, 25, 10, Color.Lime);
                DrawText($"{baseTriangles}", statX + 180, 25, 10, Color.Lime);
                DrawText("TOTAL", statX, 40, 10, Color.Lime);
                DrawText($"{totalVertices}", statX + 90, 40, 10, Color.Lime);
                DrawText($"{totalTriangles}", statX + 180, 40, 10, Color.Lime);

                if (GuiButton(new Rectangle(10, screenHeight - 80.0f, 100, 40), showModel ? "Hide Model" : "Show Model"))
                    showModel = !showModel;

                if (GuiButton(new Rectangle(120, screenHeight - 80.0f, 100, 40), "Clear Decals"))
                {
                    for (int i = 0; i < decalCount; i++) UnloadModel(decals[i]);
                    decalCount = 0;
                }

                DrawText("Left click: place decal   |   Right drag: orbit camera", 10, screenHeight - 25, 10, Color.DarkGray);
                DrawFPS(10, 10);

            EndDrawing();
        }

        for (int i = 0; i < decalCount; i++) UnloadModel(decals[i]);

        UnloadModel(placementCube);
        UnloadTexture(decalTexture);
        UnloadTexture(modelTexture);
        UnloadModel(model);

        CloseWindow();
    }

    // raygui's button, as ImGui's: a window of its own, without padding or a background, holding a
    // button as large as the rectangle, at raygui's text size of 10. Its id is the rectangle's place,
    // since the label changes.
    private static bool GuiButton(Rectangle bounds, string text)
    {
        ImGui.SetNextWindowPos(new Vector2(bounds.X, bounds.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.Begin($"##button{bounds.X},{bounds.Y}", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.PopStyleVar();
        ImGui.SetWindowFontScale(10.0f/13.0f);
        bool pressed = ImGui.Button(text, new Vector2(bounds.Width, bounds.Height));
        ImGui.End();
        return pressed;
    }

    // Appends a triangle to a growable mesh builder buffer, growing it in fixed-size chunks
    private static void AddTriangleToMeshBuilder(MeshBuilder mb, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        if (mb.vertices.Length <= (mb.vertexCount + 3))
            Array.Resize(ref mb.vertices, (1 + (mb.vertices.Length/256))*256);

        mb.vertices[mb.vertexCount++] = v0;
        mb.vertices[mb.vertexCount++] = v1;
        mb.vertices[mb.vertexCount++] = v2;
    }

    // Converts a mesh builder's triangle soup into an uploaded mesh. raylib's has no normals,
    // which its unlit default shader does not read, and a model here is lit, so each triangle
    // takes its face's normal.
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

    // Clips the segment [v0, v1] against the plane p.x = s, returning the intersection point
    private static Vector3 ClipSegment(Vector3 v0, Vector3 v1, Vector3 p, float s)
    {
        float d0 = Vector3.Dot(v0, p) - s;
        float d1 = Vector3.Dot(v1, p) - s;
        float t = d0/(d0 - d1);

        return Vector3.Lerp(v0, v1, t);
    }

    // Builds a decal mesh: the model's triangles are transformed into the decal's local space
    // (so the decal sits at the origin, facing +Z) and clipped against a decalSize-sided box,
    // following the same clip-space projection idea used by engines' decal systems (and by
    // three.js' DecalGeometry, which the technique is commonly traced back to). What's left
    // after clipping becomes the decal's own small mesh, UV-mapped from its local coordinates
    private static ModelMesh GenMeshDecal(Model model, Matrix4x4 projection, float decalSize, float decalOffset)
    {
        Matrix4x4.Invert(projection, out Matrix4x4 invProj);
        MeshBuilder[] meshBuilders = [new(), new()];
        int mbIndex = 0;

        // Gather triangles that land anywhere near the decal box, a loose, cheap pre-filter
        // meant to skip most of the model, the precise clip happening below
        for (int meshIndex = 0; meshIndex < model.Meshes.Length; meshIndex++)
        {
            // The mesh's triangles, three corners each, as raylib reads a mesh without indices
            Vector3[] corners = GetMeshComponent(model.Meshes[meshIndex]).Positions;

            for (int tri = 0; tri < corners.Length/3; tri++)
            {
                Span<Vector3> vertices = [corners[3*tri], corners[3*tri + 1], corners[3*tri + 2]];

                int insideCount = 0;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 v = Vector3.Transform(vertices[i], projection);
                    if ((MathF.Abs(v.X) < decalSize) || (MathF.Abs(v.Y) <= decalSize) || (MathF.Abs(v.Z) <= decalSize)) insideCount++;
                    vertices[i] = v;
                }

                if (insideCount > 0) AddTriangleToMeshBuilder(meshBuilders[mbIndex], vertices[0], vertices[1], vertices[2]);
            }
        }

        // Clip the surviving triangles against each of the decal box's 6 faces in turn
        Vector3[] planes =
        [
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        ];

        for (int face = 0; face < 6; face++)
        {
            mbIndex = 1 - mbIndex;

            MeshBuilder inMesh = meshBuilders[1 - mbIndex];
            MeshBuilder outMesh = meshBuilders[mbIndex];

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
                int total = (v1Out ? 1 : 0) + (v2Out ? 1 : 0) + (v3Out ? 1 : 0);

                switch (total)
                {
                    case 0:
                        // Whole triangle is inside this face, keep it as-is
                        AddTriangleToMeshBuilder(outMesh, inMesh.vertices[i], inMesh.vertices[i + 1], inMesh.vertices[i + 2]);
                        break;
                    case 1:
                        // One corner is outside, so it is clipped off, turning the triangle into a quad
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
                        break;
                    case 2:
                        // Two corners are outside, so only the small corner near the surviving vertex remains
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
                        break;
                    default:
                        // All 3 corners are outside this face, the triangle is fully clipped away
                        break;
                }
            }
        }

        MeshBuilder finalMesh = meshBuilders[mbIndex];
        ModelMesh decalMesh = default;

        if (finalMesh.vertexCount > 0)
        {
            finalMesh.uvs = new Vector2[finalMesh.vertexCount];

            for (int i = 0; i < finalMesh.vertexCount; i++)
            {
                // Clipped coordinates run roughly (-decalSize/2 .. decalSize/2), remapped to (0..1)
                finalMesh.uvs[i].X = (finalMesh.vertices[i].X/decalSize + 0.5f);
                finalMesh.uvs[i].Y = (finalMesh.vertices[i].Y/decalSize + 0.5f);

                // Nudge slightly along the normal so the decal doesn't z-fight with the surface
                finalMesh.vertices[i].Z -= decalOffset;
                finalMesh.vertices[i] = Vector3.Transform(finalMesh.vertices[i], invProj);
            }

            decalMesh = BuildMesh(finalMesh);
        }

        return decalMesh;
    }
}
