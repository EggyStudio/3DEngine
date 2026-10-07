using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

// The frames of the scene's distance field: a scene its occlusion and the sun's contact shadows
// are read from, and its first cascade drawn as it holds that scene.
public sealed partial class ReferenceFrameTests
{
    // A wall, a pillar and two crates, one on the other, on the ground under a low sun, the camera
    // looking into the corner they make, settled into the field and built before the capture.
    private void DrawFieldScene(Action<Model, Model, Model, Model> draw)
    {
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.6f, -0.55f, -0.45f)), new Color(255, 240, 220), 2, castsShadows: true);
        SetAmbientLight(new Color(150, 170, 205), 0.7f);
        SetSceneField(2, 0.25f, 2);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var wall = LoadModelFromMesh(GenMeshCube(6, 2.5f, 0.4f));
        var pillar = LoadModelFromMesh(GenMeshCylinder(0.3f, 2.5f, 16));
        var crate = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        draw(ground, wall, pillar, crate);
        UnloadModel(ground);
        UnloadModel(wall);
        UnloadModel(pillar);
        UnloadModel(crate);
    }

    private static void DrawCorner(Model ground, Model wall, Model pillar, Model crate)
    {
        ClearBackground(new Color(150, 180, 215));
        BeginMode3D(new Camera3D(new Vector3(3.2f, 2.3f, 3.4f), new Vector3(-0.3f, 0.9f, -1.3f), Vector3.UnitY, 45));
        DrawModel(ground, Vector3.Zero, 1, new Color(200, 195, 182));
        DrawModel(wall, new Vector3(0, 1.25f, -2.5f), 1, new Color(215, 200, 182));
        DrawModel(pillar, new Vector3(1.6f, 0, -0.6f), 1, new Color(232, 228, 218));
        DrawModel(crate, new Vector3(-1.2f, 0.5f, -1.6f), 1, new Color(160, 118, 78));
        DrawModelEx(crate, new Vector3(-1.1f, 1.35f, -1.7f), Vector3.UnitY, 20, new Vector3(0.7f), new Color(140, 104, 70));
        EndMode3D();
    }

    [NeedsVulkanFact]
    public void A_Scene_Lit_Through_Its_Distance_Field_Matches_Its_Reference()
    {
        Open(256, 160);
        SetAmbientOcclusion(1, 1.2f);
        DrawFieldScene((ground, wall, pillar, crate) =>
        {
            var frame = Capture(() => DrawCorner(ground, wall, pillar, crate), settle: SceneFieldPlan.SettleFrames + 6);
            Matches(frame, "scene_field_lit");
        });
    }

    [NeedsVulkanFact]
    public void A_Cascade_Of_The_Distance_Field_Matches_Its_Reference()
    {
        Open(256, 160);
        DrawFieldScene((ground, wall, pillar, crate) =>
        {
            GetApp().World.Resource<SceneFieldSettings>().Shown = 0;
            var frame = Capture(() => DrawCorner(ground, wall, pillar, crate), settle: SceneFieldPlan.SettleFrames + 6);
            Matches(frame, "scene_field_cascade");
        });
    }
}
