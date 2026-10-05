namespace Engine;

/// <summary>
/// Registers the BepuPhysics v2 implementation of <see cref="PhysicsWorld"/>, the per-frame
/// physics step in <see cref="Stage.PreUpdate"/>, the transform write-back in <see cref="Stage.PostUpdate"/>,
/// and disposes the simulation in <see cref="Stage.Cleanup"/>.
/// </summary>
/// <remarks>
/// Add explicitly via <c>app.AddPlugin(new PhysicsPlugin())</c>; included in
/// <see cref="DefaultPlugins"/> so games get rigid-body physics out-of-the-box.
/// To override <see cref="PhysicsSettings"/>, insert the resource before adding this plugin:
/// <code>
/// app.World.InsertResource(new PhysicsSettings { Gravity = new Vector3(0, -20f, 0) });
/// app.AddPlugin(new PhysicsPlugin());
/// </code>
/// </remarks>
/// <example>
/// <code>
/// [Behavior]
/// public partial struct Player
/// {
///     public PhysicsBody Body;
///
///     [OnStartup]
///     public static void Spawn(BehaviorContext ctx)
///     {
///         var e = ctx.Ecs.Spawn();
///         var body = ctx.Physics.CreateCapsule(new Vector3(0, 5, 0), 0.5f, 1f);
///         ctx.Ecs.Add(e, new Player { Body = body });
///     }
/// }
/// </code>
/// </example>
public sealed class PhysicsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Physics.Bepu");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("PhysicsPlugin: initialising BepuPhysics v2 backend...");
        var settings = app.World.GetOrInsertResource(() => new PhysicsSettings());
        var world = new PhysicsWorld(settings);
        app.World.InsertResource(world);
        // The solver's code for each pair of shapes and kind of joint compiled on a worker, before
        // a game's first contacts would compile it on a frame.
        Task.Run(() =>
        {
            try { PhysicsWorld.WarmUp(); }
            catch (Exception ex) { Logger.Warn($"PhysicsPlugin: warming the solver failed, which costs the first contacts a frame: {ex.Message}"); }
        });
        // The solver's code for each pair of shapes compiled on a worker, before a game's first
        // contacts would compile it on a frame.
        Task.Run(() =>
        {
            try { PhysicsWorld.WarmUp(); }
            catch (Exception ex) { Logger.Warn($"PhysicsPlugin: warming the solver failed, which only costs the first contacts a frame: {ex.Message}"); }
        });

        // With FixedTime (TimePlugin adds it), the simulation advances one step per FixedUpdate run,
        // on the same steps as [OnFixedUpdate] behaviors, so a behavior that pushes a body pushes it
        // once per step. Without it, PhysicsWorld keeps its own accumulator and steps in PreUpdate.
        // Bodies for the entities a scene or the program describes with a Collider and a RigidBody,
        // after SceneSpawnSystem in this stage has spawned a scene's entities.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(PhysicsBodies.Run, "Physics.SpawnBodies")
            .Write<EcsWorld>()
            .Write<PhysicsWorld>()
            .MainThreadOnly());

        // Contacts are sent as events after each step and kept until the next frame starts, so
        // code in any stage of the frame reads every contact of the frame's steps once.
        app.AddSystem(Stage.First, new SystemDescriptor(static w =>
            {
                w.ClearEvents<ContactStarted>();
                w.ClearEvents<ContactEnded>();
            }, "Physics.ClearContacts")
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>());

        app.AddSystem(Stage.FixedUpdate, new SystemDescriptor(static w =>
            {
                if (!w.TryGetResource<FixedTime>(out var fixedTime) || w.Resource<PhysicsSettings>().Paused) return;
                var phys = Prepared(w);
                DriveCharacters(w, phys);
                FollowParents(w, phys, (float)fixedTime.StepSeconds);
                phys.StepOnce((float)fixedTime.StepSeconds);
                ReportCharacters(w, phys);
                SendContacts(w, phys);
            }, "Physics.FixedStep")
            .Read<FixedTime>()
            .Write<PhysicsWorld>()
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>()
            .MainThreadOnly());

        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(static w =>
            {
                if (w.ContainsResource<FixedTime>() || w.Resource<PhysicsSettings>().Paused) return;
                var phys = Prepared(w);
                var time = w.Resource<Time>();
                DriveCharacters(w, phys);
                FollowParents(w, phys, (float)time.DeltaSeconds);
                phys.Step((float)time.DeltaSeconds);
                ReportCharacters(w, phys);
                SendContacts(w, phys);
            }, "Physics.Step")
            .Read<Time>()
            .Write<PhysicsWorld>()
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>()
            .MainThreadOnly());

        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(static w =>
            {
                var phys = w.Resource<PhysicsWorld>();
                var ecs = w.Resource<EcsWorld>();
                var alpha = w.Resource<PhysicsSettings>().Interpolate && w.TryGetResource<FixedTime>(out var fixedTime)
                    ? (float)fixedTime.Alpha
                    : 1f;
                phys.SyncTransforms(ecs, alpha);
            }, "Physics.SyncTransforms")
            .Read<PhysicsWorld>()
            .Write<EcsWorld>());

        Logger.Info("PhysicsPlugin: physics systems registered (FixedUpdate=Step, PostUpdate=SyncTransforms).");
    }

    // Each CharacterController's wanted walk, slope, jump, step and height, handed to its body
    // before the step. A height already reached costs a comparison.
    private static void DriveCharacters(World w, PhysicsWorld phys)
    {
        if (!w.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<CharacterController>() == 0) return;
        foreach (var row in ecs.QueryReadOnly<CharacterController, PhysicsBody>())
        {
            ref readonly var controller = ref row.C1;
            phys.MoveCharacter(row.C2, controller.Velocity);
            phys.SetCharacterMaxSlope(row.C2, controller.MaxSlope);
            if (controller.StepHeight > 0) phys.SetCharacterStepHeight(row.C2, controller.StepHeight);
            if (controller.Height > 0) phys.SetCharacterHeight(row.C2, controller.Height);
            if (controller.Jump > 0)
            {
                phys.JumpCharacter(row.C2, controller.Jump);
                ecs.GetRef<CharacterController>(row.Entity).Jump = 0;
            }
        }
    }

    // Each kinematic body under a parent moved over the step to its place under the parent, as its
    // own Transform puts it, so a platform an animated parent carries carries what stands on it.
    private static void FollowParents(World w, PhysicsWorld phys, float seconds)
    {
        if (!w.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<Parent>() == 0) return;
        foreach (var row in ecs.QueryReadOnly<PhysicsBody, Transform>())
        {
            if (row.C1.Kind != BodyKind.Kinematic) continue;
            var parent = ecs.ParentOf(row.Entity);
            if (parent == 0) continue;
            var world = TransformPropagation.ToMatrix(row.C2) * TransformPropagation.ComposedWorldMatrix(ecs, parent);
            if (System.Numerics.Matrix4x4.Decompose(world, out _, out var rotation, out var position))
                phys.FollowPose(row.C1, position, rotation, seconds);
        }
    }

    // Whether each controller's body stood on ground, written only when it changed.
    private static void ReportCharacters(World w, PhysicsWorld phys)
    {
        if (!w.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<CharacterController>() == 0) return;
        foreach (var row in ecs.QueryReadOnly<CharacterController, PhysicsBody>())
        {
            var grounded = phys.IsCharacterGrounded(row.C2);
            if (grounded != row.C1.Grounded) ecs.GetRef<CharacterController>(row.Entity).Grounded = grounded;
        }
    }

    // The physics world, able to name the entities of the contacts its next step finds.
    private static PhysicsWorld Prepared(World w)
    {
        var phys = w.Resource<PhysicsWorld>();
        if (phys.EntityHandle is null && w.TryGetResource<EcsWorld>(out var ecs))
            phys.EntityHandle = ecs.Handle;
        return phys;
    }

    // The lists are the physics world's own, so two apps stepping at once, as test classes run,
    // never share them.
    private static void SendContacts(World w, PhysicsWorld phys)
    {
        var (started, ended) = phys.TakePendingContacts();
        if (started.Count > 0)
        {
            var events = Events.Get<ContactStarted>(w);
            foreach (var c in started) events.Send(new ContactStarted(c.A, c.B, c.BodyA, c.BodyB, c.Point, c.Normal, c.Speed));
        }
        if (ended.Count > 0)
        {
            var events = Events.Get<ContactEnded>(w);
            foreach (var c in ended) events.Send(new ContactEnded(c.A, c.B, c.BodyA, c.BodyB));
        }
    }
}