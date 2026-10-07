using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

// The frames of the light that bounces: the Cornell box of shaders_cornell_box, Manor's library
// lit by the afternoon sun through its windows, and the glossy hall of shaders_reflections.
public sealed partial class ReferenceFrameTests
{
    [NeedsVulkanFact]
    public void A_Cornell_Box_Lit_By_Light_That_Bounces_Matches_Its_Reference()
    {
        // The room and the light of shaders_cornell_box, at High.
        Open(256, 160);
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = new Color(255, 240, 220);
        panel.Materials[0].EmissiveIntensity = 6;
        var white = new Color(220, 220, 215);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 2.5f, 8), new Vector3(0, 2.4f, 0), Vector3.UnitY, 45));
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), white);
            DrawModelEx(slab, new Vector3(0, 5.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), white);
            DrawModelEx(slab, new Vector3(0, 2.5f, -2.65f), Vector3.UnitY, 0, new Vector3(5.6f, 5.6f, 0.3f), white);
            DrawModelEx(slab, new Vector3(-2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(200, 30, 30));
            DrawModelEx(slab, new Vector3(2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(30, 170, 40));
            DrawModelEx(slab, new Vector3(-0.9f, 1.5f, -0.8f), Vector3.UnitY, 20, new Vector3(1.4f, 3, 1.4f), white);
            DrawModelEx(slab, new Vector3(1, 0.7f, 0.7f), Vector3.UnitY, -18, new Vector3(1.4f, 1.4f, 1.4f), white);
            DrawModelEx(panel, new Vector3(0, 4.97f, 0), Vector3.UnitY, 0, new Vector3(1.6f, 0.06f, 1.6f), Color.White);
            EndMode3D();
        }, settle: SceneFieldPlan.SettleFrames + 10);
        Matches(frame, "cornell_box");
        UnloadModel(slab);
        UnloadModel(panel);
    }

    [NeedsVulkanFact]
    public void A_Room_Of_A_Game_Lit_By_The_Sun_Through_Its_Windows_Matches_Its_Reference()
    {
        // Manor's library, its furniture and its lamps placed as the game's level places them, under
        // the game's sky and a sun lower than the game's, through whose windows the sun falls on the
        // floor and bounces with the lamps' light to the ceiling and the walls.
        Open(256, 160);
        SetSceneField(2, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.Medium);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.25f, -0.45f, -0.85f)), new Color(255, 244, 226), 2.4f, castsShadows: true);
        SetShadowDistance(24);
        SetAmbientLight(new Color(150, 175, 215), 0.3f);
        var sky = GenImageColor(256, 128, Color.Blank);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(70, 120, 210), new Color(205, 225, 245)), 0, 0, Color.White);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(120, 140, 110), new Color(60, 72, 60)), 0, 64, Color.White);
        SetEnvironmentMap(sky, intensity: 0.6f);
        CreatePointLight(new Vector3(0, 3.4f, 0), new Color(255, 214, 160), 5, range: 14);
        CreatePointLight(new Vector3(6.4f, 0.8f, 0), new Color(255, 140, 60), 6, range: 9);
        var pieces = new (string Name, Vector3 At, float Turn)[]
        {
            ("room-library", Vector3.Zero, 0), ("fireplace", new(7.3f, 0, 0), -90), ("bookcase", new(-4.5f, 0, -7.6f), 0),
            ("bookcase", new(4.5f, 0, -7.6f), 0), ("bookcase", new(-7.6f, 0, 4.5f), 90), ("sofa", new(2, 0, 0), -90),
            ("rug", new(4, 0, 0), 90), ("table", new(4, 0, 4), 0),
        };
        var models = pieces.Select(p => p.Name).Distinct().ToDictionary(name => name, name => LoadModel($"resources/manor/{name}.obj"));

        var frame = Capture(() =>
        {
            ClearBackground(new Color(70, 120, 210));
            BeginMode3D(new Camera3D(new Vector3(-2, 2.2f, -6.5f), new Vector3(1, 0.6f, 5), Vector3.UnitY, 70));
            foreach (var (name, at, turn) in pieces)
                DrawModelEx(models[name], at, Vector3.UnitY, turn, Vector3.One, Color.White);
            EndMode3D();
        }, settle: SceneFieldPlan.SettleFrames + 10);
        Matches(frame, "lit_room");
        foreach (var model in models.Values) UnloadModel(model);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void Glossy_Surfaces_Reflecting_Through_The_Field_Match_Their_Reference()
    {
        // The hall of shaders_reflections at High: a polished floor, a chrome ball and a gold one,
        // three pillars and a glowing strip, under a low sun and a sky.
        Open(256, 160);
        SetSceneField(3, 0.2f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -0.6f, -0.4f)), new Color(255, 240, 220), 2.5f, castsShadows: true);
        var sky = GenImageColor(256, 128, Color.Blank);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(50, 95, 170), new Color(190, 210, 235)), 0, 0, Color.White);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(90, 95, 85), new Color(40, 42, 38)), 0, 64, Color.White);
        SetEnvironmentMap(sky, intensity: 0.5f);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(40, 40, 46)) { Roughness = 0.08f };
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        wall.Materials[0] = new ModelMaterial(new Color(200, 196, 188));
        var pillar = LoadModelFromMesh(GenMeshCylinder(0.35f, 3, 24));
        var chrome = LoadModelFromMesh(GenMeshSphere(0.7f, 48, 48));
        chrome.Materials[0] = new ModelMaterial(new Color(235, 235, 235)) { Metallic = 1, Roughness = 0.05f };
        var gold = LoadModelFromMesh(GenMeshSphere(0.6f, 48, 48));
        gold.Materials[0] = new ModelMaterial(new Color(255, 200, 90)) { Metallic = 1, Roughness = 0.3f };
        var strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        strip.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(120, 200, 255), EmissiveIntensity = 4 };

        var frame = Capture(() =>
        {
            ClearBackground(new Color(50, 95, 170));
            BeginMode3D(new Camera3D(new Vector3(0, 2.2f, 7.5f), new Vector3(0, 0.9f, 0), Vector3.UnitY, 50));
            DrawSkybox();
            DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(12, 0.2f, 12), Color.White);
            DrawModelEx(wall, new Vector3(0, 2, -4), Vector3.UnitY, 0, new Vector3(12, 4, 0.3f), Color.White);
            DrawModelEx(strip, new Vector3(0, 2.6f, -3.8f), Vector3.UnitY, 0, new Vector3(5, 0.25f, 0.1f), Color.White);
            DrawModel(pillar, new Vector3(-3, 0, -2), 1, new Color(200, 40, 40));
            DrawModel(pillar, new Vector3(0, 0, -2.6f), 1, new Color(40, 170, 60));
            DrawModel(pillar, new Vector3(3, 0, -2), 1, new Color(50, 80, 210));
            DrawModel(chrome, new Vector3(-1.1f, 0.7f, 0.4f), 1, Color.White);
            DrawModel(gold, new Vector3(1.4f, 0.6f, 0.8f), 1, Color.White);
            EndMode3D();
        }, settle: SceneFieldPlan.SettleFrames + 10);
        Matches(frame, "reflections");
        foreach (var model in new[] { floor, wall, pillar, chrome, gold, strip }) UnloadModel(model);
        UnloadEnvironmentMap();
    }
}
