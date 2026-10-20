using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering.Models;

/// <summary>
/// An <see cref="AnimationMachine"/> choosing the clip of an arm's <see cref="AnimatedModel"/>,
/// <c>arm.gltf</c> with a second clip, "rest", which holds the elbow straight beside "bend", which
/// turns it a quarter over a second, posed on the CPU in an app with no renderer.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class AnimationMachineTests : IDisposable
{
    private readonly App _app = new();
    private readonly EcsWorld _ecs = new();
    private readonly Time _time = new() { MaxDeltaSeconds = 10 };
    private readonly TestFolder _folder = new("engine-machine-");
    private readonly string _arm;
    private double _elapsed;

    public AnimationMachineTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<MeshStore>();
        _app.World.InitResource<ModelDrawList>();
        _app.World.InitResource<DrawList>();
        _app.World.InsertResource(_ecs);
        _app.World.InsertResource(_time);
        UseApp(_app);
        var camera = _ecs.Spawn();
        _ecs.Add(camera, new Camera(60f));
        _ecs.Add(camera, new Transform(new Vector3(0, 1, 6)));
        _arm = AnimatedModelDrawsTests.TwoClips(_folder);
    }

    public void Dispose()
    {
        UseApp(null);
        _folder.Dispose();
    }

    private void Frame(double seconds = 0.1)
    {
        _elapsed += seconds;
        _time.Update(_elapsed, seconds);
        _app.World.Resource<ModelDrawList>().Clear();
        AnimatedModelDraws.Run(_app.World);
    }

    private int Spawn(AnimationMachine machine)
    {
        var entity = _ecs.Spawn();
        _ecs.Add(entity, Transform.Identity);
        _ecs.Add(entity, new AnimatedModel(_arm));
        _ecs.Add(entity, machine);
        return entity;
    }

    private static AnimationStateMachine Machine(params AnimationMachineTransition[] transitions) => new()
    {
        States =
        [
            new() { Name = "Rest", Clip = "rest" },
            new() { Name = "Bend", Clip = "bend" },
            new() { Name = "Once", Clip = "bend", Loop = false },
        ],
        Transitions = [.. transitions],
    };

    private string State(int entity) => _ecs.GetReadOnly<AnimationMachine>(entity).State;
    private ref AnimationMachine Parameters(int entity) => ref _ecs.GetRef<AnimationMachine>(entity);
    private AnimatedModel Model(int entity) => _ecs.GetReadOnly<AnimatedModel>(entity);

    // The arm's tip as the frame drew it, in the model's own space.
    private Vector3 Tip()
    {
        var draw = _app.World.Resource<ModelDrawList>().Draws.Single();
        _app.World.Resource<MeshStore>().TryGetData(draw.Mesh, out var vertices, out _).Should().BeTrue();
        return vertices.Select(v => v.Position).MaxBy(p => p.LengthSquared());
    }

    [Fact]
    public void A_Value_Past_Its_Bound_Moves_The_Machine_And_Its_Fade_Is_The_Model_s_Blend()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new() { From = "Rest", To = "Bend", When = AnimationCondition.Above, Parameter = "speed", Value = 1, Fade = 0.25f },
            new() { From = "Bend", To = "Rest", When = AnimationCondition.Below, Parameter = "speed", Value = 0.5f, Fade = 0 })));

        Frame();
        State(entity).Should().Be("Rest", "the machine starts in its first state once the model has loaded");
        Model(entity).Clip.Should().Be("rest");
        Frame();
        State(entity).Should().Be("Rest", "a speed never set is 0");

        Parameters(entity).Set("speed", 2f);
        Frame();
        State(entity).Should().Be("Bend");
        Model(entity).Should().Match<AnimatedModel>(m => m.Clip == "bend" && m.BlendSeconds == 0.25f && Math.Abs(m.Time - 0.1f) < 1e-4f,
            "the clip plays from its start, blended from the one before over the transition's fade");

        Parameters(entity).Set("speed", 0.2f);
        Frame();
        State(entity).Should().Be("Rest");
        Model(entity).BlendSeconds.Should().Be(0, "a fade of 0 cuts");
    }

    [Fact]
    public void A_Flag_Moves_The_Machine_Each_Way()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new() { From = "Rest", To = "Bend", When = AnimationCondition.True, Parameter = "crouch" },
            new() { From = "Bend", To = "Rest", When = AnimationCondition.False, Parameter = "crouch" })));

        Frame();
        Parameters(entity).Set("crouch", true);
        Frame();
        State(entity).Should().Be("Bend");
        Frame();
        State(entity).Should().Be("Bend", "the flag still holds");
        Parameters(entity).Set("crouch", false);
        Frame();
        State(entity).Should().Be("Rest");
    }

    [Fact]
    public void A_Trigger_Is_Used_Up_By_The_Transition_That_Takes_It()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new() { From = "Rest", To = "Bend", When = AnimationCondition.Triggered, Parameter = "jump" },
            new() { From = "Bend", To = "Rest", When = AnimationCondition.Triggered, Parameter = "jump" })));

        Frame();
        Parameters(entity).Trigger("jump");
        Frame();
        State(entity).Should().Be("Bend");
        Frame();
        State(entity).Should().Be("Bend", "the trigger was used up taking the first transition");
        Parameters(entity).Trigger("jump");
        Frame();
        State(entity).Should().Be("Rest");
    }

    [Fact]
    public void A_Clip_That_Plays_Once_Holds_Its_End_And_Its_End_Moves_The_Machine_From_Any_State()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new() { To = "Once", When = AnimationCondition.Triggered, Parameter = "go", Fade = 0 },
            new() { From = "Once", To = "Bend", When = AnimationCondition.Finished, Fade = 0 })));

        Frame();
        Parameters(entity).Trigger("go");
        Frame(0.25);
        State(entity).Should().Be("Once", "a transition from any state is taken from Rest");
        Frame(0.25);
        Frame(0.25);
        Model(entity).Speed.Should().Be(1, "three quarters in");
        Frame(0.25);
        State(entity).Should().Be("Once", "the clip has reached its end and holds there this frame");
        Model(entity).Speed.Should().Be(0);
        Tip().X.Should().BeApproximately(-1, 0.03f, "held at the clip's last frame, the elbow turned its quarter, not come round to the start");

        Frame(0.25);
        State(entity).Should().Be("Bend", "the clip's end moves the machine");
        Model(entity).Speed.Should().Be(1);
    }

    [Fact]
    public void A_Looping_Clip_Finishes_As_It_Comes_Round_Once()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new AnimationMachineTransition { From = "Rest", To = "Bend", When = AnimationCondition.Finished, Fade = 0 })));

        Frame();
        for (int i = 0; i < 9; i++) Frame(0.1);
        State(entity).Should().Be("Rest", "the second of rest has not come round");
        Frame(0.1);
        Frame(0.1);
        State(entity).Should().Be("Bend");
    }

    [Fact]
    public void A_Machine_Saved_To_A_File_Reads_Back_And_Runs_From_It_And_A_Scene_Names_It()
    {
        var machine = Machine(
            new() { From = "Rest", To = "Bend", When = AnimationCondition.Above, Parameter = "speed", Value = 1.5f, Fade = 0.3f },
            new() { To = "Once", When = AnimationCondition.Triggered, Parameter = "hit", Fade = 0 });
        machine.Start = "Bend";
        var path = _folder.File("arm.machine.json");
        machine.Save(path);

        var text = File.ReadAllText(path);
        text.Should().Contain("\"format\": \"3dengine-animation-machine\"").And.Contain("\"When\": \"Triggered\"");
        var read = AnimationStateMachine.Load(path);
        text.Should().MatchRegex("\"version\": 1,\\s*\"id\": \"[0-9a-f]{12}\"", "the save gives the file an id after its format and version");
        read.Write().Should().Be(System.Text.RegularExpressions.Regex.Replace(text, "\\r?\\n\\s*\"id\": \"[0-9a-f]{12}\",", ""),
            "what was read writes the same file, the id aside, which the save gives and Write leaves out");
        read.Start.Should().Be("Bend");
        read.States.Select(s => (s.Name, s.Clip, s.Loop, s.Speed)).Should().Equal(machine.States.Select(s => (s.Name, s.Clip, s.Loop, s.Speed)));
        read.Transitions.Select(t => (t.From, t.To, t.When, t.Parameter, t.Value, t.Fade))
            .Should().Equal(machine.Transitions.Select(t => (t.From, t.To, t.When, t.Parameter, t.Value, t.Fade)));

        var entity = Spawn(new AnimationMachine(path));
        Frame();
        State(entity).Should().Be("Bend", "the file's start");

        var scene = SceneFile.Write(_ecs, [entity]);
        scene.Should().Contain("\"AnimationMachine\"").And.Contain("arm.machine.json").And.Contain("\"State\": \"Bend\"");
        var world = new World();
        world.InsertResource(new EcsWorld());
        var loaded = SceneFile.Read(world, scene)[0];
        var named = world.Resource<EcsWorld>().GetReadOnly<AnimationMachine>(loaded);
        (named.Path, named.State).Should().Be((path, "Bend"));
    }

    [Fact]
    [ExpectsError("Engine.Models", "enters a state the machine does not have")]
    public void A_Machine_Whose_Names_Do_Not_Hold_Together_Runs_Nothing_And_Says_So_Once()
    {
        var entity = Spawn(new AnimationMachine(Machine(new AnimationMachineTransition { From = "Rest", To = "Jump", When = AnimationCondition.True, Parameter = "x" })));

        Frame();
        Frame();

        State(entity).Should().BeEmpty();
        Model(entity).Clip.Should().BeEmpty("the model plays its clip as it was given");
    }

    [Fact]
    public void A_Step_With_No_Transition_Due_Reads_The_Parameters_And_Changes_And_Allocates_Nothing()
    {
        var entity = Spawn(new AnimationMachine(Machine(
            new() { From = "Rest", To = "Bend", When = AnimationCondition.Above, Parameter = "speed", Value = 1 },
            new() { From = "Rest", To = "Bend", When = AnimationCondition.True, Parameter = "crouch" },
            new() { To = "Once", When = AnimationCondition.Triggered, Parameter = "jump" })));
        Frame();
        Parameters(entity).Set("speed", 0.5f);
        var machines = new AnimationMachines();
        var clips = LoadModelAnimations(_arm);
        var animated = Model(entity);
        machines.Step(_ecs, entity, ref animated, clips, 0.016f);
        var before = animated;

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) machines.Step(_ecs, entity, ref animated, clips, 0.016f);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        allocated.Should().Be(0);
        animated.Should().Be(before, "no transition was due, so the model's clip, speed, time and blend are as they were");
        State(entity).Should().Be("Rest");
    }
}
