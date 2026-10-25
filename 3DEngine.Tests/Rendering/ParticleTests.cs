using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Particle emitters, stepped by a compute shader and drawn after the window's meshes, read from
/// chosen pixels of frames drawn offscreen.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ParticleTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-particles-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        // Each frame a sixtieth of a second, so particles age by the frames drawn and not the machine.
        var config = Config.Default.WithWindow("particle test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames in 3D mode, through the test's camera or another, the last of them captured.
    private Image Capture(int frames, Action? draw = null, Camera3D? camera = null)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera ?? _camera);
            draw?.Invoke();
            EndMode3D();
            if (frame == frames - 1) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    // A cloud of still particles around the middle of the picture, emitted at once, laid over
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

        Sum(before).Should().Be(0, "an emitter not emitting shows nothing");
        ((int)after.R).Should().BeGreaterThan(after.G + 60, $"the burst draws red particles over the middle, not {after}");
    }

    [NeedsVulkanFact]
    public void Empty_Emitters_Are_Neither_Stepped_Nor_Drawn_And_A_Burst_Costs_Its_Own_Particles_Until_They_Die()
    {
        Open();
        // Twenty with room for ten thousand each, as a voxel game's ambient emitters stand, none emitting.
        var emitters = Enumerable.Range(0, 20)
            .Select(i => CreateParticleEmitter(new Vector3((i - 3) * 0.1f, 0, 0), Cloud(Color.White) with { MaxParticles = 10_000, Life = 0.5f, LifeVariation = 0.5f }))
            .ToArray();
        var particles = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ParticleRenderer>();

        Capture(3);
        (particles.Stepped, particles.Drawn).Should().Be((0, 0), "no particle was born");

        EmitParticles(emitters[3], 400);
        var burst = Capture(2);
        (particles.Stepped, particles.Drawn).Should().Be((400, 400), "the burst's slots alone are stepped, sorted and drawn");
        Sum(GetImageColor(burst, 80, 60)).Should().BeGreaterThan(300, "the burst shows");

        // The longest life is three quarters of a second, 45 frames, after which none is left.
        Capture(50);
        (particles.Stepped, particles.Drawn).Should().Be((0, 0), "the burst's particles died");
    }

    [NeedsVulkanFact]
    public void A_Particle_Facing_Up_Lies_Flat_Seen_From_Above_And_Not_Edge_On_Where_One_Facing_The_Camera_Is_Seen_From_Both()
    {
        Open();
        var above = new Camera3D(new Vector3(0, 6, 0), Vector3.Zero, new Vector3(0, 0, -1), 45);
        // One large dot at the middle, the test's own camera level with it and so edge-on to a flat one.
        (int Above, int EdgeOn) Seen(ParticleEmitter settings)
        {
            var emitter = CreateParticleEmitter(Vector3.Zero, Cloud(new Color(255, 40, 20)) with
            {
                MaxParticles = 1, Radius = 0, StartSize = 1.5f, EndSize = 1.5f,
                Facing = settings.Facing, Normal = settings.Normal, Velocity = settings.Velocity, Spread = 0, SpeedVariation = 0,
            });
            EmitParticles(emitter, 1);
            var seen = (Sum(GetImageColor(Capture(3, camera: above), 80, 60)), Sum(GetImageColor(Capture(1), 80, 60)));
            UnloadParticleEmitter(emitter);
            return seen;
        }

        var camera = Seen(default);
        camera.Above.Should().BeGreaterThan(200, "a dot facing the camera is seen from above");
        camera.EdgeOn.Should().BeGreaterThan(200, "and from beside it");

        var flat = Seen(new ParticleEmitter { Facing = ParticleFacing.Normal, Normal = Vector3.UnitY });
        flat.Above.Should().BeCloseTo(camera.Above, 30, "a dot facing up is seen whole from above");
        flat.EdgeOn.Should().BeLessThan(30, "and edge-on from beside it, where it covers nothing");

        // Rising too slowly to leave the middle in the frames drawn, one facing its velocity faces up.
        var rising = Seen(new ParticleEmitter { Facing = ParticleFacing.Velocity, Velocity = new Vector3(0, 0.001f, 0) });
        rising.Above.Should().BeCloseTo(camera.Above, 30);
        rising.EdgeOn.Should().BeLessThan(30);
    }

    [NeedsVulkanFact]
    public void A_Reflection_Probe_Captures_The_Particles_Around_It()
    {
        Open();
        var room = LoadModelFromMesh(GenMeshCube(6, 6, 6));
        room.Materials[0] = new ModelMaterial(new Color(20, 20, 20)) { DoubleSided = true };
        var cloud = CreateParticleEmitter(new Vector3(2, 0, 0), Cloud(new Color(255, 30, 30)) with { Radius = 0.4f });
        EmitParticles(cloud, 400);
        var probe = CreateReflectionProbe(Vector3.Zero, new Vector3(6, 6, 6));
        for (int frame = 0; frame < 120 && !IsReflectionProbeReady(probe); frame++) Capture(1, () => DrawModel(room, Vector3.Zero, 1, Color.White));
        IsReflectionProbeReady(probe).Should().BeTrue();

        // The middle of the first mip's +X face, a mirror's view of the cloud.
        var map = GetApp().World.Resource<ReflectionProbes>().ByEntity.Values.Single().Map!;
        var texels = ((GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!).ReadCubeFaces(map);
        var at = ((int)map.Size * (int)map.Size / 2 + (int)map.Size / 2) * 4;
        ((float)texels[at]).Should().BeGreaterThan(4 * (float)texels[at + 2], "the probe sees the red cloud between it and the wall");
        UnloadReflectionProbe(probe);
        UnloadModel(room);
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
        ecs.GetReadOnly<ParticleEmitter>(entity).Burst.Should().Be(0, "a burst is emitted once");
    }

    // Still particles of one color that add their light, born only by a burst, each 0.6 wide.
    private static ParticleEmitter Still(Color color) => ParticleEmitter.Default with
    {
        Emitting = false,
        MaxParticles = 20,
        Life = 30,
        LifeVariation = 0,
        Velocity = Vector3.Zero,
        SpeedVariation = 0,
        Gravity = Vector3.Zero,
        Radius = 0,
        StartSize = 0.6f,
        EndSize = 0.6f,
        StartColor = color,
        EndColor = color,
    };

    // An emitter one and a half units along x from a parent at the middle, which the camera at 6
    // units sees at pixel 116, 60 of the 160 by 120 frame, and a quarter turn of the parent about
    // the view's axis carries to 80, 24.
    private static (EcsWorld Ecs, int Parent, int Emitter) Parented(ParticleEmitter emitter)
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var parent = ecs.Spawn();
        ecs.Add(parent, new Transform(Vector3.Zero));
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(new Vector3(1.5f, 0, 0)));
        ecs.Add(entity, new Parent(ecs.Handle(parent)));
        ecs.Add(entity, emitter);
        return (ecs, parent, entity);
    }

    private static readonly Quaternion QuarterTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);

    [NeedsVulkanFact]
    public void Particles_In_Their_Emitters_Space_Turn_With_Its_Parent_Where_Those_In_The_World_Stay()
    {
        Open();
        var (ecs, parent, inSpace) = Parented(Still(new Color(30, 255, 40)) with { Local = true, Burst = 10 });
        var inWorld = ecs.Spawn();
        ecs.Add(inWorld, new Transform(new Vector3(1.5f, 0, 0)));
        ecs.Add(inWorld, new Parent(ecs.Handle(parent)));
        ecs.Add(inWorld, Still(new Color(40, 60, 255)) with { Burst = 10 });
        var before = Capture(3);
        ecs.GetRef<Transform>(parent).Rotation = QuarterTurn;
        var after = Capture(3);

        var right = GetImageColor(before, 116, 60);
        ((int)right.G).Should().BeGreaterThan(120, $"both emitters' particles start at the emitter, green and blue, not {right}");
        ((int)right.B).Should().BeGreaterThan(120, $"both emitters' particles start at the emitter, green and blue, not {right}");
        var top = GetImageColor(after, 80, 24);
        ((int)top.G).Should().BeGreaterThan(top.B + 60, $"the particles in the emitter's space turn with its parent, so green shows above, not {top}");
        var stayed = GetImageColor(after, 116, 60);
        ((int)stayed.B).Should().BeGreaterThan(stayed.G + 60, $"those in the world keep their places, so blue alone stays at the right, not {stayed}");
    }

    [NeedsVulkanFact]
    public void A_Particle_Placed_At_A_Point_Keeps_Its_Place_On_Its_Emitter_Through_Its_Parents_Turn()
    {
        Open();
        var (ecs, parent, emitter) = Parented(Still(new Color(255, 60, 40)) with { Points = [Vector3.Zero] });
        var before = Capture(3);
        ecs.GetRef<Transform>(parent).Rotation = QuarterTurn;
        var after = Capture(3);
        ecs.GetRef<ParticleEmitter>(emitter).Points = null;
        var gone = Capture(3);

        Sum(GetImageColor(before, 116, 60)).Should().BeGreaterThan(200, "the placed particle stands at its point on the emitter");
        Sum(GetImageColor(before, 80, 24)).Should().BeLessThan(40, "and nowhere else");
        var turned = GetImageColor(after, 80, 24);
        ((int)turned.R).Should().BeGreaterThan(150, $"a quarter turn of the parent carries it above the middle, not {turned}");
        Sum(GetImageColor(after, 116, 60)).Should().BeLessThan(40, "where it stood before is left dark");
        Sum(GetImageColor(gone, 80, 24)).Should().BeLessThan(40, "it lives until the points no longer hold it");
    }

    [NeedsVulkanFact]
    public void Drag_Stops_A_Stream_Short_Of_Where_It_Would_Rise_Without()
    {
        Open();
        // The stream above rises three units in its second of life, and with a drag of 3 it comes
        // no further than one, so it is seen near the emitter and not at the middle.
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
            Drag = 3,
            StartSize = 0.3f,
            EndSize = 0.3f,
            StartColor = Color.White,
            EndColor = Color.White,
        });
        var frame = Capture(60);

        Sum(GetImageColor(frame, 80, 89)).Should().BeGreaterThan(300, "the stream leaves the emitter");
        Sum(GetImageColor(frame, 80, 60)).Should().Be(0, "drag has slowed it to a stop before the middle, a unit and a half up");
    }

    [NeedsVulkanFact]
    public void A_Textured_Particle_Is_Drawn_As_Its_Image_The_Right_Way_Up_And_Square()
    {
        Open();
        var image = GenImageColor(8, 8, Color.Blue);
        ImageDrawRectangle(ref image, 0, 0, 8, 4, Color.Red);
        var texture = LoadTextureFromImage(image);
        var emitter = CreateParticleEmitter(Vector3.Zero, Cloud(Color.White) with { MaxParticles = 1, Radius = 0, StartSize = 2, EndSize = 2, Texture = texture });
        EmitParticles(emitter, 1);
        var frame = Capture(3);

        // Two units wide is 48 pixels across the middle of the picture.
        var top = GetImageColor(frame, 80, 45);
        var bottom = GetImageColor(frame, 80, 75);
        var corner = GetImageColor(frame, 80 + 21, 60 + 21);
        ((int)top.R).Should().BeGreaterThan(top.B + 100, $"the image's top row is drawn at the top, not {top}");
        ((int)bottom.B).Should().BeGreaterThan(bottom.R + 100, $"and its bottom at the bottom, not {bottom}");
        ((int)corner.B).Should().BeGreaterThan(100, $"the whole square is the image, where the dot would have faded its corner, not {corner}");
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void An_Emitter_Whose_Texture_Is_Unloaded_Draws_On_Without_It()
    {
        // The emitter drawn alone, with no model, whose pass is the one that clears the sets the
        // model pass keeps for a frame by texture.
        Open();
        var image = GenImageColor(8, 8, Color.Blue);
        ImageDrawRectangle(ref image, 0, 0, 8, 4, Color.Red);
        var texture = LoadTextureFromImage(image);
        var emitter = CreateParticleEmitter(Vector3.Zero, Cloud(Color.White) with { MaxParticles = 1, Radius = 0, StartSize = 2, EndSize = 2, Texture = texture });
        EmitParticles(emitter, 1);
        var before = GetImageColor(Capture(3), 80, 45);
        UnloadTexture(texture);
        // Past the frames the texture's view is kept for after it goes, the emitter drawing on.
        var after = Capture(GpuTextures.RetireFrames + 4);

        ((int)before.R).Should().BeGreaterThan(before.B + 100, $"the image's top row is drawn while it is loaded, not {before}");
        foreach (var (x, y) in new[] { (80, 45), (80, 75) })
        {
            var color = GetImageColor(after, x, y);
            Math.Min(Math.Min(color.R, color.G), color.B).Should().BeGreaterThan(200, $"once it is unloaded the particle is drawn white, as with no texture, not {color} at ({x}, {y})");
        }
    }

    [NeedsVulkanFact]
    public void A_Particle_Plays_Through_The_Frames_Of_A_Sheet_Over_Its_Life()
    {
        Open();
        // Two frames side by side, red then blue, over a life of two seconds.
        var image = GenImageColor(16, 8, Color.Blue);
        ImageDrawRectangle(ref image, 0, 0, 8, 8, Color.Red);
        var texture = LoadTextureFromImage(image);
        var emitter = CreateParticleEmitter(Vector3.Zero, Cloud(Color.White) with
        {
            MaxParticles = 1, Radius = 0, StartSize = 2, EndSize = 2, Life = 2, LifeVariation = 0,
            Texture = texture, TextureColumns = 2, TextureRows = 1,
        });
        EmitParticles(emitter, 1);
        // Frames a tenth of a second long, so it is about 0.3 seconds old at the first capture and
        // 1.3 at the second, with room either side for the frames a capture waits.
        GetApp().World.Resource<Time>().FrameSeconds = 0.1;
        var early = GetImageColor(Capture(3), 80, 60);
        var late = GetImageColor(Capture(10), 80, 60);

        ((int)early.R).Should().BeGreaterThan(early.B + 100, $"early in its life it shows the first frame, not {early}");
        ((int)late.B).Should().BeGreaterThan(late.R + 100, $"late in its life the second, not {late}");
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Sheet_Whose_Frames_Blend_Mixes_In_The_Next_Frame_As_A_Life_Goes_On()
    {
        Open();
        // Red then blue, over a life of two seconds, on two particles born together, the left one
        // blending its frames and the right one not.
        var image = GenImageColor(16, 8, Color.Blue);
        ImageDrawRectangle(ref image, 0, 0, 8, 8, Color.Red);
        var texture = LoadTextureFromImage(image);
        ParticleEmitter Sheet(bool blend) => Cloud(Color.White) with
        {
            MaxParticles = 1, Radius = 0, StartSize = 1, EndSize = 1, Life = 2, LifeVariation = 0,
            Texture = texture, TextureColumns = 2, TextureRows = 1, BlendFrames = blend,
        };
        var blended = CreateParticleEmitter(new Vector3(-1, 0, 0), Sheet(blend: true));
        var cut = CreateParticleEmitter(new Vector3(1, 0, 0), Sheet(blend: false));
        EmitParticles(blended, 1);
        EmitParticles(cut, 1);
        // About 0.3 seconds old, three tenths of the way through the first frame's share.
        GetApp().World.Resource<Time>().FrameSeconds = 0.1;
        var frame = Capture(3);
        var mixed = GetImageColor(frame, 56, 60);
        var held = GetImageColor(frame, 104, 60);

        ((int)held.R).Should().BeGreaterThan(held.B + 100, $"a sheet that does not blend shows its first frame alone, not {held}");
        ((int)mixed.B).Should().BeGreaterThan(held.B + 40, $"one that blends has some of the next frame's blue in it, not {mixed} beside {held}");
        ((int)mixed.R).Should().BeGreaterThan(mixed.B, $"and is still more of the first frame than of the next, not {mixed}");
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void Particles_Thrown_At_A_Wall_Pass_Through_It_Bounce_Back_Off_It_Or_End_There_As_Their_Emitter_Says()
    {
        Open();
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // Red particles from in front of a gray wall, thrown straight at it, through it on the
        // left, bouncing off it in the middle and ending at it on the right.
        ParticleEmitter Thrown(ParticleCollision collision) => Cloud(new Color(255, 40, 20)) with
        {
            MaxParticles = 60, Radius = 0.3f, StartSize = 0.25f, EndSize = 0.25f,
            Velocity = new Vector3(0, 0, -3), Spread = 0, Collision = collision, Bounce = 0.5f,
        };
        var through = CreateParticleEmitter(new Vector3(-1.5f, 0, 1.5f), Thrown(ParticleCollision.None));
        var bounced = CreateParticleEmitter(new Vector3(0, 0, 1.5f), Thrown(ParticleCollision.Bounce));
        var ended = CreateParticleEmitter(new Vector3(1.5f, 0, 1.5f), Thrown(ParticleCollision.Die));
        foreach (var emitter in new[] { through, bounced, ended }) EmitParticles(emitter, 60);

        // About a second and a half, by when every one has reached the wall, 0.8 seconds away.
        var frame = Capture(90, () => DrawModelEx(wall, new Vector3(0, 0, -1), Vector3.UnitY, 0, new Vector3(8, 6, 0.2f), new Color(70, 70, 70)));
        int Red(int middle)
        {
            var count = 0;
            for (int y = 40; y < 80; y++)
                for (int x = middle - 14; x <= middle + 14; x++)
                    if (GetImageColor(frame, x, y) is var c && c.R > c.G + 100) count++;
            return count;
        }

        Red(44).Should().Be(0, "particles that collide with nothing went through the wall and are hidden behind it");
        Red(80).Should().BeGreaterThan(20, "particles that bounce came back off the wall and are in front of it");
        Red(116).Should().Be(0, "particles that end at the wall are gone");
        UnloadModel(wall);
    }

    [NeedsVulkanFact]
    public void Of_Two_Clouds_Laid_Over_By_Alpha_The_Nearer_Is_In_Front_Whichever_Was_Made_First()
    {
        Open();
        // The near cloud made first, which drawn in the order made would be covered by the far one.
        var near = CreateParticleEmitter(new Vector3(0, 0, 1.5f), Cloud(new Color(255, 30, 30)) with { Radius = 0.2f });
        var far = CreateParticleEmitter(new Vector3(0, 0, -1.5f), Cloud(new Color(30, 30, 255)) with { Radius = 0.2f });
        EmitParticles(near, 400);
        EmitParticles(far, 400);
        var middle = GetImageColor(Capture(3), 80, 60);

        ((int)middle.R).Should().BeGreaterThan(middle.B + 100, $"the nearer red cloud covers the farther blue one, not {middle}");
    }

    [NeedsVulkanFact]
    public void Particles_Are_Drawn_Into_A_Render_Texture_Through_Its_Camera()
    {
        Open();
        var target = LoadRenderTexture(160, 120);
        // Only a shape drawn into it in 3D, whose camera the particles are drawn through.
        var cloud = CreateParticleEmitter(Vector3.Zero, Cloud(new Color(255, 30, 30)) with { Radius = 0.3f });
        EmitParticles(cloud, 400);

        var path = Path.Combine(_folder.Path, "target.png");
        for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawCube(new Vector3(0, -2, 0), 0.05f, 0.05f, 0.05f, Color.Gray);
            EndMode3D();
            EndTextureMode();
            // The target shown in the window's top left quarter, its middle at (40, 30).
            ClearBackground(Color.Black);
            DrawTexturePro(target.Texture, new Rectangle(0, 0, 160, -120), new Rectangle(0, 0, 80, 60), Vector2.Zero, 0, Color.White);
            if (frame == 3) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue();
        var middle = GetImageColor(LoadImage(path), 40, 30);
        ((int)middle.R).Should().BeGreaterThan(middle.G + 60, $"the red cloud is in the render texture, not {middle}");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
        UnloadRenderTexture(target);
    }

    // The stream of the test below, blue as it is born at the back and red as it ages toward the
    // front, drawn into two render textures from either end of it, each shown in a half of the
    // window, and each lays the particles over far to near from its own camera, so the one at the
    // front shows the red in front and the one behind the blue.
    [NeedsVulkanFact]
    public void One_Cloud_Drawn_Into_Two_Views_From_Either_Side_Is_Laid_Over_In_Each_Ones_Order()
    {
        var config = Config.Default.WithWindow("particle test", 320, 120) with { Headless = true, Offscreen = true, Samples = 4, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateParticleEmitter(new Vector3(0, 0, -3), ParticleEmitter.Default with
        {
            Blend = ParticleBlend.Alpha,
            MaxParticles = 300,
            Rate = 150,
            Life = 2,
            LifeVariation = 0,
            Velocity = new Vector3(0, 0, 2),
            Spread = 0,
            SpeedVariation = 0,
            Gravity = Vector3.Zero,
            StartSize = 0.6f,
            EndSize = 0.6f,
            StartColor = new Color(30, 30, 255),
            EndColor = new Color(255, 30, 30),
        });
        Camera3D[] cameras = [new(new Vector3(0, 0, 6), new Vector3(0, 0, -1), Vector3.UnitY, 45), new(new Vector3(0, 0, -8), new Vector3(0, 0, -1), Vector3.UnitY, 45)];
        var views = cameras.Select(_ => LoadRenderTexture(160, 120)).ToArray();
        var path = Path.Combine(_folder.Path, "views.png");
        for (int frame = 0; frame < 100 && !File.Exists(path); frame++)
        {
            for (int v = 0; v < 2; v++)
            {
                BeginTextureMode(views[v]);
                ClearBackground(Color.Black);
                BeginMode3D(cameras[v]);
                DrawCube(new Vector3(0, -3, -1), 0.05f, 0.05f, 0.05f, Color.Gray);
                EndMode3D();
                EndTextureMode();
            }
            BeginDrawing();
            ClearBackground(Color.Black);
            for (int v = 0; v < 2; v++) DrawTextureRec(views[v].Texture, new Rectangle(0, 0, 160, -120), new Vector2(160 * v, 0), Color.White);
            if (frame == 89) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue();
        var image = LoadImage(path);
        var (front, back) = (GetImageColor(image, 80, 60), GetImageColor(image, 240, 60));
        ((int)front.R).Should().BeGreaterThan(front.B + 60, $"from the front the nearest, oldest particles are drawn last, not {front}");
        ((int)back.B).Should().BeGreaterThan(back.R + 60, $"from behind the nearest, youngest particles are drawn last, not {back}");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
        foreach (var view in views) UnloadRenderTexture(view);
    }

    // A stream coming straight at the camera, blue as it is born far away and red as it ages near,
    // every particle over the middle of the picture, so the nearest, red, is in front. 300 sorts in
    // one block of shared memory, and 2000 across four, through the steps between blocks.
    [NeedsVulkanTheory]
    [InlineData(300)]
    [InlineData(2000)]
    public void The_Particles_Of_One_Cloud_Laid_Over_By_Alpha_Are_Drawn_Far_To_Near(int count)
    {
        Open();
        CreateParticleEmitter(new Vector3(0, 0, -3), ParticleEmitter.Default with
        {
            Blend = ParticleBlend.Alpha,
            MaxParticles = count,
            Rate = count / 2f,
            Life = 2,
            LifeVariation = 0,
            Velocity = new Vector3(0, 0, 2),
            Spread = 0,
            SpeedVariation = 0,
            Gravity = Vector3.Zero,
            StartSize = 0.6f,
            EndSize = 0.6f,
            StartColor = new Color(30, 30, 255),
            EndColor = new Color(255, 30, 30),
        });
        var middle = GetImageColor(Capture(90), 80, 60);

        ((int)middle.R).Should().BeGreaterThan(middle.B + 60, $"the nearest, oldest particles are drawn last, not {middle}");
    }
}
