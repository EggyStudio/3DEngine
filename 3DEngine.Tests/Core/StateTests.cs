using FluentAssertions;

namespace Engine.Tests.Core;

[Trait("Category", "Unit")]
public class StateTests
{
    private enum Screen { Menu, Playing, Paused }

    private static (App App, List<string> Log) Machine()
    {
        var app = new App();
        var log = new List<string>();
        app.AddState(Screen.Menu)
            .OnEnter(Screen.Menu, _ => log.Add("enter Menu"))
            .OnExit(Screen.Menu, _ => log.Add("exit Menu"))
            .OnEnter(Screen.Playing, _ => log.Add("enter Playing"))
            .OnExit(Screen.Playing, _ => log.Add("exit Playing"));
        return (app, log);
    }

    [Fact]
    public void The_First_Value_Is_Entered_On_The_First_Frame_After_Startup()
    {
        var (app, log) = Machine();
        app.AddSystem(Stage.Startup, _ => log.Add("startup"));

        app.Frame();
        app.Frame();

        log.Should().Equal("startup", "enter Menu");
    }

    [Fact]
    public void A_Queued_Move_Exits_The_Old_Value_Then_Enters_The_New_One_Next_Frame()
    {
        var (app, log) = Machine();
        app.Frame();
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Playing);
        app.World.Resource<State<Screen>>().Current.Should().Be(Screen.Menu, "a move waits for the transition point");
        app.Frame();

        log.Should().Equal("exit Menu", "enter Playing");
        var state = app.World.Resource<State<Screen>>();
        state.Current.Should().Be(Screen.Playing);
        state.Previous.Should().Be(Screen.Menu);
        app.World.Resource<NextState<Screen>>().Pending.Should().BeNull();
    }

    [Fact]
    public void A_Move_To_The_Same_Value_Runs_Nothing()
    {
        var (app, log) = Machine();
        app.Frame();
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Menu);
        app.Frame();

        log.Should().BeEmpty();
    }

    [Fact]
    public void A_Move_Queued_In_Update_Is_Seen_By_Update_The_Next_Frame()
    {
        var app = new App();
        var seen = new List<Screen>();
        app.AddState(Screen.Menu);
        app.AddSystem(Stage.Update, new SystemDescriptor(world =>
        {
            seen.Add(world.Resource<State<Screen>>().Current);
            world.Resource<NextState<Screen>>().Set(Screen.Playing);
        }).MainThreadOnly());

        app.Frame();
        app.Frame();

        seen.Should().Equal(Screen.Menu, Screen.Playing);
    }

    [Fact]
    public void InState_Runs_A_System_Only_While_The_State_Holds()
    {
        var app = new App();
        var runs = 0;
        app.AddState(Screen.Menu);
        app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs++).RunIf(BehaviorConditions.InState(Screen.Playing)).MainThreadOnly());

        app.Frame();
        app.World.Resource<NextState<Screen>>().Set(Screen.Playing);
        app.Frame();
        app.Frame();

        runs.Should().Be(2);
    }

    [Fact]
    public void InState_Fails_For_A_State_Never_Added()
    {
        BehaviorConditions.InState(Screen.Menu)(new World()).Should().BeFalse();
    }

    [Fact]
    public void Transition_Systems_Registered_Before_The_State_Is_Added_Still_Run()
    {
        var app = new App();
        var entered = false;
        app.OnEnter(Screen.Paused, _ => entered = true);
        app.AddState(Screen.Paused);

        app.Frame();

        entered.Should().BeTrue();
    }

    [Fact]
    public void An_Entity_Spawned_On_Enter_Is_There_For_Update_In_The_Same_Frame()
    {
        var app = new App();
        new EcsPlugin().Build(app);
        var counted = -1;
        app.AddState(Screen.Menu)
            .OnEnter(Screen.Menu, world => world.Resource<EcsCommands>().Spawn((e, ecs) => ecs.Add(e, new Name("title"))))
            .AddSystem(Stage.Update, new SystemDescriptor(world => counted = world.Resource<EcsWorld>().Count<Name>()).MainThreadOnly());

        app.Frame();

        counted.Should().Be(1);
    }

    [Fact]
    public void A_State_Can_Be_Read_And_Moved_By_Name()
    {
        var (app, log) = Machine();
        app.Frame();
        log.Clear();
        var transitions = app.World.Resource<StateTransitions>();

        var (state, current, values) = transitions.Describe(app.World).Should().ContainSingle().Subject;
        (state, current).Should().Be(("Screen", "Menu"));
        values.Should().Equal("Menu", "Playing", "Paused");
        transitions.TryQueue(app.World, "screen", "playing").Should().BeNull();
        transitions.TryQueue(app.World, "Screen", "Nowhere").Should().Contain("Menu, Playing, Paused");
        transitions.TryQueue(app.World, "Mode", "Menu").Should().Contain("No state is called 'Mode'");
        app.Frame();

        log.Should().Equal("exit Menu", "enter Playing");
    }

    [Fact]
    public void Conditions_Added_Twice_Must_Both_Pass()
    {
        var world = new World();
        var system = new SystemDescriptor(_ => { }).RunIf(_ => true).RunIf(_ => false);

        system.RunCondition!(world).Should().BeFalse();
    }

    private enum Pause { Running, Stopped }
    private enum InGame { Yes }

    [Fact]
    public void Each_Move_Is_An_Event_Until_The_Next_Frame_Begins()
    {
        var (app, _) = Machine();
        app.AddSystem(Stage.First, new SystemDescriptor(_ => { }, "First"));
        app.Frame();
        app.World.ReadEvents<StateTransition<Screen>>().Should().Equal(new StateTransition<Screen>(null, Screen.Menu));

        app.World.Resource<NextState<Screen>>().Set(Screen.Playing);
        app.Frame();
        app.World.ReadEvents<StateTransition<Screen>>().Should().Equal(new StateTransition<Screen>(Screen.Menu, Screen.Playing));

        app.Frame();
        app.World.ReadEvents<StateTransition<Screen>>().Should().BeEmpty("a frame with no move clears the last one's");
    }

    [Fact]
    public void A_Sub_State_Exists_Only_While_Its_Parent_Is_In_Its_Value()
    {
        var (app, log) = Machine();
        app.AddSubState(Screen.Playing, Pause.Running)
            .OnEnter(Pause.Running, _ => log.Add("enter Running"))
            .OnExit(Pause.Stopped, _ => log.Add("exit Stopped"));
        app.Frame();
        app.World.ContainsResource<State<Pause>>().Should().BeFalse("the menu has no pause");
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Playing);
        app.Frame();
        log.Should().Equal("exit Menu", "enter Playing", "enter Running");
        app.World.Resource<State<Pause>>().Current.Should().Be(Pause.Running);

        app.World.Resource<NextState<Pause>>().Set(Pause.Stopped);
        app.Frame();
        app.World.Resource<State<Pause>>().Current.Should().Be(Pause.Stopped);
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Menu);
        app.Frame();
        log.Should().Equal("exit Stopped", "exit Playing", "enter Menu");
        app.World.ContainsResource<State<Pause>>().Should().BeFalse("leaving play takes the pause away");
        app.World.ReadEvents<StateTransition<Pause>>().Should().Equal(new StateTransition<Pause>(Pause.Stopped, null));
    }

    [Fact]
    public void A_Computed_State_Follows_Its_Source_And_Moves_Only_When_Its_Value_Does()
    {
        var (app, log) = Machine();
        app.AddComputedState<InGame, Screen>(screen => screen == Screen.Menu ? null : InGame.Yes)
            .OnEnter(InGame.Yes, _ => log.Add("enter InGame"))
            .OnExit(InGame.Yes, _ => log.Add("exit InGame"));
        app.Frame();
        app.World.ContainsResource<State<InGame>>().Should().BeFalse();
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Playing);
        app.Frame();
        log.Should().Equal("exit Menu", "enter Playing", "enter InGame");
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Paused);
        app.Frame();
        log.Should().Equal("exit Playing").And.NotContain("exit InGame", "playing and paused are both in game");
        app.World.Resource<State<InGame>>().Current.Should().Be(InGame.Yes);
        log.Clear();

        app.World.Resource<NextState<Screen>>().Set(Screen.Menu);
        app.Frame();
        log.Should().Equal("enter Menu", "exit InGame");
        app.World.ContainsResource<State<InGame>>().Should().BeFalse();
    }
}
