using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A world a program moves by a whole step (<see cref="MoveWorld"/>), drawn the frame before the
/// move and the frames after it, the distance field's builds counted through the move.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class WorldMoveTests : IDisposable
{
    private const int Width = 160, Height = 96;
    private readonly TestFolder _folder = new("engine-world-move-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // A thousand blocks over a floor at a place, sixty-four of them mesh entities, lit by the sun
    // and a lamp, with a reflection probe over them and a burst of particles standing still in the
    // world's space.
    private sealed class Scene
    {
        private readonly Model _block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        private readonly List<int> _entities = [];
        private readonly ReflectionProbeHandle _probe;
        private readonly ParticleEmitterHandle _emitter;
        public Vector3 At;

        public Scene(Vector3 at)
        {
            At = at;
            var ecs = GetApp().World.Resource<EcsWorld>();
            for (int i = 0; i < 64; i++)
            {
                var entity = ecs.Spawn();
                ecs.Add(entity, new Mesh(_block.Meshes[0]));
                ecs.Add(entity, new Material(new Vector4(0.3f + i % 4 * 0.15f, 0.7f, 0.5f, 1)));
                ecs.Add(entity, new Transform(Place(i)));
                _entities.Add(entity);
            }
            _probe = CreateReflectionProbe(At + new Vector3(0, 2, 0), new Vector3(24, 6, 24));
            _emitter = CreateParticleEmitter(At + new Vector3(-5, 1.5f, 6), ParticleEmitter.Default with
            {
                MaxParticles = 40, Emitting = false, Burst = 40, Life = 600, Velocity = Vector3.Zero, Gravity = Vector3.Zero, Local = false,
                Radius = 0.75f, StartSize = 0.25f, EndSize = 0.25f, StartColor = new Color(220, 220, 230), EndColor = new Color(220, 220, 230),
            });
        }

        private Vector3 Place(int i) => At + new Vector3(i % 8 * 2 - 7, 2.5f, i / 8 * 1.5f - 3);

        // Every place the scene hands the engine moved by the step, the entities', the probe's and
        // the emitter's with the blocks drawn.
        public void Move(Vector3 step)
        {
            At += step;
            var ecs = GetApp().World.Resource<EcsWorld>();
            for (int i = 0; i < _entities.Count; i++) ecs.Update(_entities[i], new Transform(Place(i)));
            ecs.Update(_probe.Entity.Index, new Transform(At + new Vector3(0, 2, 0)));
            SetParticleEmitterPosition(_emitter, At + new Vector3(-5, 1.5f, 6));
        }

        public void Frame(string? capture = null)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(At + new Vector3(0, 9, 18), At + new Vector3(0, 0, 2), Vector3.UnitY, 60));
            SetSceneFieldCenter(At);
            DrawModelEx(_block, At + new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(48, 1, 48), new Color(200, 190, 170));
            for (int i = 0; i < 1000; i++)
            {
                var (x, z) = (i % 40 - 19.5f, i / 40 - 12.5f);
                DrawModelEx(_block, At + new Vector3(x, 0.25f + i % 3 * 0.25f, z), Vector3.UnitY, i % 7 * 10, new Vector3(0.5f, 0.5f + i % 3 * 0.5f, 0.5f),
                    new Color((byte)(120 + i % 5 * 25), 140, (byte)(200 - i % 4 * 30)));
            }
            EndMode3D();
            if (capture is not null) TakeScreenshot(capture);
            EndDrawing();
        }

        public void Unload() => UnloadModel(_block);
    }

    private static SceneFieldPlan Plan() => GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<SceneFieldRenderer>().Plan!;

    // The frame drawn now captured, its pixels' levels in sRGB's 255ths, the frames after it drawn
    // until the capture is written.
    private float[] Captured(Scene scene)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        scene.Frame(path);
        for (int frame = 0; frame < 8 && !File.Exists(path); frame++) scene.Frame();
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        return [.. LoadImageColors(LoadImage(path)).Select(c => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B)];
    }

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_World_Moved_By_A_Whole_Step_Keeps_Its_Field_And_Its_Light_And_Draws_As_Before(GlobalIllumination quality)
    {
        var config = Config.Default.WithWindow("world move", Width, Height) with { Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.25f);
        SetGlobalIllumination(quality);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f, castsShadows: true);
        var at = new Vector3(8192, 0, -4096);
        var lamp = CreatePointLight(at + new Vector3(3, 3, 2), new Color(255, 200, 150), 6, range: 10, castsShadows: true);
        var scene = new Scene(at);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 60; frame++) scene.Frame();
        var plan = Plan();
        var still = plan.StillCount;
        var probe = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ReflectionProbes>().ByEntity.Values.Single();
        var captured = probe.CapturedAt;
        var before = Captured(scene);

        // Every place the scene hands the engine moved back by a step of 4,096, the world told of
        // it, and the frame after it read as the frame before.
        var step = new Vector3(-4096, 0, 0);
        scene.Move(step);
        SetLightPosition(lamp, at + step + new Vector3(3, 3, 2));
        MoveWorld(step);
        var after = Captured(scene);
        var splatted = new List<int> { Plan().Splatted };
        for (int frame = 0; frame < 6; frame++)
        {
            scene.Frame();
            splatted.Add(Plan().Splatted);
        }

        ReferenceEquals(Plan(), plan).Should().BeTrue("the field is the one made before the move");
        Plan().StillCount.Should().Be(still, "every mesh at its matrix moved by the step is the same still mesh, where without the move none is");
        splatted.Should().OnlyContain(n => n == 0, "no cascade is built again");
        probe.CapturedAt.Should().Be(captured, "the reflection probe moved with the world is not captured again");
        var apart = before.Zip(after, (a, b) => MathF.Abs(a - b)).ToArray();
        apart.Max().Should().BeLessThan(1.5f, "the frame after the move reads as the frame before, where without it 8,486 pixels moved by up to 209 levels");
        scene.Unload();
    }
}
