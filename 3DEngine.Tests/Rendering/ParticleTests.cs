using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Particle emitters, stepped by a compute shader and drawn after the window's meshes, read from
/// chosen pixels of frames drawn offscreen.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ParticleTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-particles-").FullName;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("particle test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames in 3D mode, the last of them captured.
    private Image Capture(int frames, Action? draw = null)
    {
        var path = Path.Combine(_directory, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            draw?.Invoke();
            EndMode3D();
            if (frame == frames - 1) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    // A cloud of still particles around the middle of the picture, given off at once, laid over
    // each other by alpha so a dense one keeps its color where added light would turn white.
    private static ParticleEmitter Cloud(Color color) => ParticleEmitter.Default with
    {
        Blend = ParticleBlend.Alpha,
        MaxParticles = 400,
        Emitting = false,
        Life = 30,
        Velocity = Vector3.Zero,
        Gravity = Vector3.Zero,
        Radius = 0.8f,
        StartSize = 0.4f,
        EndSize = 0.4f,
        StartColor = color,
        EndColor = color,
    };

    private static int Sum(Color c) => c.R + c.G + c.B;

    [NeedsVulkanFact]
    public void A_Burst_Of_Unlit_Particles_Shows_In_Its_Color_Where_None_Showed_Before()
    {
        Open();
        var emitter = CreateParticleEmitter(Vector3.Zero, Cloud(new Color(255, 40, 20)));
        var before = GetImageColor(Capture(3), 80, 60);
        EmitParticles(emitter, 400);
        var after = GetImageColor(Capture(3), 80, 60);

        Sum(before).Should().Be(0, "an emitter not emitting gives off nothing");
        ((int)after.R).Should().BeGreaterThan(after.G + 60, $"the burst draws red particles over the middle, not {after}");
    }

    [NeedsVulkanFact]
    public void A_Stream_Rises_At_Its_Velocity_And_Fades_As_It_Dies()
    {
        Open();
        CreateParticleEmitter(new Vector3(0, -1.5f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 800,
            Rate = 300,
            Life = 1,
            LifeVariation = 0,
            Velocity = new Vector3(0, 3, 0),
            Spread = 0,
            SpeedVariation = 0,
            Gravity = Vector3.Zero,
            StartSize = 0.3f,
            EndSize = 0.3f,
            StartColor = Color.White,
            EndColor = Color.White,
        });
        var frame = Capture(60);

        // A column of particles from the emitter up three units, a second at three a second, and
        // nothing above where they die nor to the side.
        Sum(GetImageColor(frame, 80, 70)).Should().BeGreaterThan(300, "the column passes through the middle");
        Sum(GetImageColor(frame, 80, 10)).Should().Be(0, "particles die a second, three units, above the emitter");
        Sum(GetImageColor(frame, 30, 60)).Should().Be(0, "and none stray to the side with no spread");
    }

    [NeedsVulkanFact]
    public void A_Lit_Particle_Takes_The_Light_Around_It_Where_An_Unlit_One_Gives_Off_Its_Own()
    {
        Open();
        var lit = CreateParticleEmitter(new Vector3(-1.2f, 0, 0), Cloud(Color.White) with { Lit = true, Radius = 0.3f });
        var unlit = CreateParticleEmitter(new Vector3(1.2f, 0, 0), Cloud(Color.White) with { Radius = 0.3f });
        EmitParticles(lit, 200);
        EmitParticles(unlit, 200);
        var light = CreatePointLight(new Vector3(-1.2f, 0, 2), new Color(40, 120, 255), 3);
        var frame = Capture(4);

        var litColor = GetImageColor(frame, 47, 60);
        var unlitColor = GetImageColor(frame, 113, 60);
        ((int)litColor.B).Should().BeGreaterThan(litColor.R + 40, $"the lit cloud is blue in the blue light, not {litColor}");
        unlitColor.R.Should().BeGreaterThan(200, $"the unlit cloud keeps its white, not {unlitColor}");
        UnloadLight(light);
    }

    [NeedsVulkanFact]
    public void An_Emitter_In_The_Ecs_Is_Drawn_As_One_Made_Through_The_Flat_Api()
    {
        Open();
        var ecs = GetApp().World.Resource<EcsWorld>();
        var entity = ecs.Spawn();
        ecs.Add(entity, Cloud(new Color(30, 255, 40)) with { Burst = 300 });
        ecs.Add(entity, new Transform(Vector3.Zero));
        var color = GetImageColor(Capture(3), 80, 60);

        ((int)color.G).Should().BeGreaterThan(color.R + 60, $"the entity's burst is drawn green, not {color}");
        ecs.GetReadOnly<ParticleEmitter>(entity).Burst.Should().Be(0, "a burst is given off once");
    }
}
