using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The descriptor pools the renderer makes, each asked for no more than the sets it was counted
/// for, as the validation layer reads them where it runs, through frames that use every one.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class DescriptorPoolTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-pools-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void No_Pool_Is_Asked_For_A_Set_Past_Its_Counts()
    {
        // The scene field's pool was counted by hand, six storage images where a build's two sets
        // took nine once its resolve pass read the faces, which Mesa's drivers allocated and
        // MoltenVK refused, so the fault failed frames on macOS alone. The layer's warning on it is
        // counted with its errors. The frames here draw the sets of every pool, those of the
        // field's build, of its stamp of a moving block and of its repaint of a mesh recolored, of
        // the probes' cascades at High and the screen's probes with a glow light, of a reflection
        // probe's and the sky's filters, of a mesh's morph on the GPU, of particles, of the shared
        // pools and, where the device traces rays, of the reference.
        var config = Config.Default.WithWindow("pool test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        var sky = GenImageColor(64, 32, new Color(90, 120, 200));
        SetEnvironmentMap(sky);
        UnloadImage(sky);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        glow.Materials[0].Emissive = Color.White;
        glow.Materials[0].EmissiveIntensity = 6;
        var morph = LoadModel(Path.Combine(AppContext.BaseDirectory, "resources", "morph.gltf"));
        SetModelMorphWeight(morph, "Raise", 0.5f);
        var probe = CreateReflectionProbe(new Vector3(0, 1, 0), new Vector3(6, 3, 6));
        var particles = CreateParticleEmitter(new Vector3(1, 1, 0));
        var camera = new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50);

        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            if (frame == SceneFieldPlan.SettleFrames + 10) UpdateMeshBuffer(slab.Meshes[0], 3, Enumerable.Repeat(new Color(200, 60, 60), 24).ToArray(), 0);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -2), Vector3.UnitY, 0, new Vector3(6, 3, 0.3f), Color.White);
            DrawModel(block, new Vector3(MathF.Sin(frame * 0.2f), 0.4f, 1), 0.4f, Color.White);
            DrawModel(glow, new Vector3(-1.5f, 0.5f, 0), 0.5f, Color.White);
            DrawModel(morph, new Vector3(1.5f, 0, 0), 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        if (Probes.RayQuery.Value)
            BounceReference.Trace(GetApp().World, Path.Combine(_folder.Path, "reference.png"), 4).Should().StartWith("traced");

        IsReflectionProbeReady(probe).Should().BeTrue("the probe's faces were drawn and filtered");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("no set is allocated past its pool's counts");
        UnloadParticleEmitter(particles);
        UnloadReflectionProbe(probe);
        UnloadModel(morph);
        UnloadModel(glow);
        UnloadModel(block);
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [ExpectsError("Engine.Schedule", "RenderPlugin.Render")]
    public void Frames_That_Fail_Leave_What_The_Frames_Before_Them_Read_Until_Those_Have_Finished()
    {
        // A frame that fails is not submitted and takes no slot of the frames in flight, while the
        // scene field counts it among the frames it keeps a run's pool for, so after three that
        // failed it destroyed the pool of the frame submitted before them, which the device might
        // still be running and the layer found in use. A block moving through the field gives every
        // frame a pool of its own.
        var config = Config.Default.WithWindow("failing frames", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var failing = new Failing();
        var graph = GetApp().World.Resource<Engine.Renderer>().Graph;
        graph.AddNode("failing", failing);
        graph.AddNodeEdge("scene_field", "failing");
        var camera = new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 0.5f, 0), Vector3.UnitY, 50);

        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++)
        {
            if (frame == SceneFieldPlan.SettleFrames + 6) failing.Left = 3;
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModel(block, new Vector3(MathF.Sin(frame * 0.2f), 0.5f, 0), 0.5f, Color.White);
            EndMode3D();
            EndDrawing();
        }

        failing.Left.Should().Be(0, "the three frames failed");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("no pool is destroyed while a submitted frame binds its sets");
        UnloadModel(block);
    }

    // A node that throws while it has frames left to fail, as one whose pool ran out did.
    private sealed class Failing : INode
    {
        public int Left;

        public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
        {
            if (Left <= 0) return;
            Left--;
            throw new InvalidOperationException("The frame fails on purpose.");
        }
    }
}
