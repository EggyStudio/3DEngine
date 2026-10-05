using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.Engine3D;

// Swarm, holding out in an arena against waves of creatures that close in from every side, with a
// gun that fires at the nearest. Unlike the other games here it is written in the ECS, so the
// loop below only opens and closes frames, and every creature, shot, wave and the HUD is a
// behavior. `Swarm --build` writes the creatures' prefabs, which are committed.
if (args.Contains("--build"))
{
    Prefabs.Build(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "resources", "prefabs"));
    return;
}

SetConfigFlags(ConfigFlags.Msaa4xHint);
InitWindow(1280, 720, "Swarm");
SetTargetFPS(60);
SetBloom(0.6f);
InitAudioDevice();
Swarm.Start();

while (!WindowShouldClose())
{
    BeginDrawing();
    ClearBackground(new Color(16, 18, 26));
    EndDrawing();
}

CloseWindow();

// -- The game's states.

/// <summary>The screens, from the title through play to its end.</summary>
public enum Screen { Title, Playing, Over }

/// <summary>Within play, a wave being fought or the breather after it.</summary>
[SubStateOf(Screen.Playing)]
public enum Round { Fighting, Break }

// -- Resources the behaviors share.

/// <summary>Where a game stands, which the rules write and the HUD shows.</summary>
public sealed class Arena
{
    public int Wave;
    public int Score;
    public int Kills;
    public int ToSpawn;
    public float SpawnTimer;
    public float BreakTimer;
    public float Health = 100;
    public Vector3 PlayerAt;
    public int Best;
}

/// <summary>
/// The numbers that set how the game plays, which the script in source/behaviors writes every
/// frame, so they are tuned by saving that file while the game runs.
/// </summary>
public sealed class Tuning
{
    public float PlayerSpeed = 6;
    public float FireInterval = 0.14f;
    public float ShotSpeed = 30;
    public float EnemySpeed = 1;
    public float DamagePerSpeed = 2.2f;
    public int FirstWave = 14;
    public int MorePerWave = 10;
}

/// <summary>Sounds, each with a few aliases taking turns, so many of one play over each other.</summary>
public sealed class Sounds
{
    private readonly Dictionary<string, (Sound[] Voices, int Next)> _byName = [];

    public void Load(string name, int voices, float volume)
    {
        var sound = LoadSound($"resources/sounds/{name}.wav");
        var all = new Sound[voices];
        all[0] = sound;
        for (int i = 1; i < voices; i++) all[i] = LoadSoundAlias(sound);
        foreach (var voice in all) SetSoundVolume(voice, volume);
        _byName[name] = (all, 0);
    }

    public void Play(string name, float pitch = 1)
    {
        var (voices, next) = _byName[name];
        SetSoundPitch(voices[next], pitch);
        PlaySound(voices[next]);
        _byName[name] = (voices, (next + 1) % voices.Length);
    }
}

/// <summary>The meshes made once and shared, so every copy of a shape is one upload.</summary>
public static class Shapes
{
    public static Mesh Ball = default, Cube = default, Player = default;

    public static void Make()
    {
        Ball = GetMeshComponent(GenMeshSphere(1, 8, 12));
        Cube = GetMeshComponent(GenMeshCube(1, 1, 1));
        Player = GetMeshComponent(GenMeshCylinder(0.45f, 1.6f, 16));
    }
}

/// <summary>Sets the game up before its first frame.</summary>
public static class Swarm
{
    public const float Radius = 22;

    public static void Start()
    {
        var app = GetApp();
        app.World.InsertResource(new Arena());
        app.World.InsertResource(new Tuning());
        var sounds = new Sounds();
        sounds.Load("shoot", 8, 0.35f);
        sounds.Load("hit", 12, 0.5f);
        sounds.Load("pop", 12, 0.6f);
        sounds.Load("hurt", 3, 0.8f);
        sounds.Load("wave", 1, 0.7f);
        sounds.Load("over", 1, 0.8f);
        app.World.InsertResource(sounds);
        Shapes.Make();
        AddState(Screen.Title);
    }

    // The point of the floor an entity stands on, from its body.
    public static Vector3 Flat(Vector3 at) => new(at.X, 0, at.Z);
}

// -- The arena, its camera and its light, made once.

[Behavior]
public struct Stage
{
    [OnStartup]
    public static void Build(BehaviorContext ctx)
    {
        var ecs = ctx.Ecs;
        var floor = ecs.Spawn();
        ecs.Add(floor, new Transform(new Vector3(0, -0.5f, 0), new Vector3(Swarm.Radius * 2 + 4, 1, Swarm.Radius * 2 + 4)));
        ecs.Add(floor, Shapes.Cube);
        ecs.Add(floor, new Material(new Color(52, 58, 70)) { RoughnessFactor = 0.9f });
        ecs.Add(floor, Collider.Box(new Vector3(Swarm.Radius * 2 + 4, 1, Swarm.Radius * 2 + 4)));
        ecs.Add(floor, RigidBody.Static);

        // A low wall around the floor, which keeps the player in and which creatures climb over.
        for (int side = 0; side < 4; side++)
        {
            var along = side < 2 ? Vector3.UnitX : Vector3.UnitZ;
            var across = side < 2 ? Vector3.UnitZ : Vector3.UnitX;
            var at = across * (side % 2 == 0 ? 1 : -1) * (Swarm.Radius + 1.5f) + new Vector3(0, 0.4f, 0);
            var size = along * (Swarm.Radius * 2 + 4) + across + new Vector3(0, 0.8f, 0);
            var wall = ecs.Spawn();
            ecs.Add(wall, new Transform(at, size));
            ecs.Add(wall, Shapes.Cube);
            ecs.Add(wall, new Material(new Color(90, 96, 110)));
            ecs.Add(wall, Collider.Box(size));
            ecs.Add(wall, RigidBody.Static);
        }

        // Pillars to dodge around, each with a lamp on top.
        for (int i = 0; i < 6; i++)
        {
            var (sin, cos) = MathF.SinCos(i * MathF.Tau / 6 + 0.3f);
            var at = new Vector3(cos, 0, sin) * 11;
            var pillar = ecs.Spawn();
            ecs.Add(pillar, new Transform(at + new Vector3(0, 1.5f, 0), new Vector3(1.4f, 3, 1.4f)));
            ecs.Add(pillar, Shapes.Cube);
            ecs.Add(pillar, new Material(new Color(110, 104, 96)));
            ecs.Add(pillar, Collider.Box(new Vector3(1.4f, 3, 1.4f)));
            ecs.Add(pillar, RigidBody.Static);

            var lamp = ecs.Spawn();
            ecs.Add(lamp, new Transform(at + new Vector3(0, 3.3f, 0), new Vector3(0.35f)));
            ecs.Add(lamp, Shapes.Ball);
            ecs.Add(lamp, new Material(Color.Black) { EmissiveFactor = new Vector3(4, 2.4f, 0.8f) });
            var light = ecs.Spawn();
            ecs.Add(light, new Transform(at + new Vector3(0, 3.6f, 0)));
            ecs.Add(light, Light.Point(new Vector3(1, 0.65f, 0.3f), 3, range: 9));
        }

        var moon = ecs.Spawn();
        ecs.Add(moon, Light.Directional(new Vector3(0.6f, 0.7f, 1), 0.9f) with { CastsShadows = true });
        ecs.Add(moon, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.5f, -1.0f, 0), Vector3.One));
        var sky = ecs.Spawn();
        ecs.Add(sky, Light.Ambient(new Vector3(0.45f, 0.5f, 0.7f), 0.35f));

        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(fovY: 50));
        ecs.Add(camera, new Transform(new Vector3(0, 20, 14)));
        ecs.Add(camera, new Follow());
    }
}

/// <summary>The camera, high behind the player and easing after it.</summary>
[Behavior]
public struct Follow
{
    [OnUpdate]
    public void Track(BehaviorContext ctx, ref Transform transform)
    {
        var target = Swarm.Flat(ctx.Res<Arena>().PlayerAt);
        var eye = target + new Vector3(0, 20, 13);
        var ease = 1 - MathF.Exp(-4 * (float)ctx.Time.DeltaSeconds);
        transform.Position = Vector3.Lerp(transform.Position, eye, ease);
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(transform.Position, transform.Position - new Vector3(0, 20, 13), Vector3.UnitY), out var world);
        transform.Rotation = Quaternion.CreateFromRotationMatrix(world);
    }
}

// -- The player.

[Behavior]
public struct Player
{
    public float Cooldown;
    public float Flash;

    /// <summary>Puts the player in the arena as play starts, gone with it as play ends.</summary>
    [OnEnter(Screen.Playing)]
    public static void Spawn(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        (arena.Wave, arena.Score, arena.Kills, arena.Health, arena.PlayerAt) = (0, 0, 0, 100, Vector3.Zero);
        var player = ctx.Ecs.Spawn();
        ctx.Ecs.Add(player, new Player());
        ctx.Ecs.Add(player, new Transform(new Vector3(0, 0.9f, 0)));
        ctx.Ecs.Add(player, Shapes.Player);
        ctx.Ecs.Add(player, new Material(new Color(90, 200, 255)) { MetallicFactor = 0.2f, RoughnessFactor = 0.4f });
        ctx.Ecs.Add(player, Collider.Capsule(0.45f, 1.8f));
        ctx.Ecs.Add(player, RigidBody.Dynamic(80));
        ctx.Ecs.Add(player, CharacterController.Default);
        ctx.Ecs.DespawnOnExit(player, Screen.Playing);
    }

    /// <summary>
    /// Walks by WASD or the arrows, and fires at the nearest creature, on the main thread since it
    /// plays a sound and writes where the player is for the creatures to read.
    /// </summary>
    [OnUpdate]
    [MainThread]
    [InState(Screen.Playing)]
    public void Move(BehaviorContext ctx, ref CharacterController controller, in PhysicsBody body, ref Material material)
    {
        var tuning = ctx.Res<Tuning>();
        var input = ctx.Input;
        var walk = Vector3.Zero;
        if (input.KeyDown(Key.W) || input.KeyDown(Key.Up)) walk.Z -= 1;
        if (input.KeyDown(Key.S) || input.KeyDown(Key.Down)) walk.Z += 1;
        if (input.KeyDown(Key.A) || input.KeyDown(Key.Left)) walk.X -= 1;
        if (input.KeyDown(Key.D) || input.KeyDown(Key.Right)) walk.X += 1;
        controller.Velocity = walk == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(walk) * tuning.PlayerSpeed;

        var at = ctx.Physics.GetPosition(body);
        ctx.Res<Arena>().PlayerAt = at;
        var dt = (float)ctx.Time.DeltaSeconds;
        Flash = MathF.Max(0, Flash - dt * 3);
        material.EmissiveFactor = new Vector3(Flash * 3, 0, 0);

        Cooldown -= dt;
        if (Cooldown > 0 || Nearest(ctx, at) is not { } target) return;
        Cooldown = tuning.FireInterval;
        // Low, at the height of a crawler's middle, which a brute's body covers too.
        var from = new Vector3(at.X, 0.5f, at.Z);
        var aim = target - from;
        aim.Y = 0;
        if (aim.LengthSquared() < 1e-4f) return;
        var shot = new Shot { Velocity = Vector3.Normalize(aim) * tuning.ShotSpeed, Life = 1.2f };
        ctx.Cmd.Spawn((entity, ecs) =>
        {
            ecs.Add(entity, shot);
            ecs.Add(entity, new Transform(from, new Vector3(0.18f)));
            ecs.Add(entity, Shapes.Ball);
            ecs.Add(entity, new Material(Color.Black) { EmissiveFactor = new Vector3(6, 5, 2) });
            ecs.Add(entity, Collider.Sphere(0.18f) with { IsTrigger = true });
            ecs.Add(entity, RigidBody.Kinematic);
            ecs.DespawnOnExit(entity, Screen.Playing);
        });
        ctx.Res<Sounds>().Play("shoot", 0.9f + Random.Shared.NextSingle() * 0.2f);
    }

    // The creature nearest the player within reach, by its body.
    private static Vector3? Nearest(BehaviorContext ctx, Vector3 at)
    {
        Vector3? best = null;
        var bestDistance = 18f * 18f;
        foreach (var row in ctx.Ecs.QueryReadOnly<Creature, PhysicsBody>())
        {
            var there = ctx.Physics.GetPosition(row.C2);
            var distance = Vector3.DistanceSquared(there, at);
            if (distance < bestDistance) (best, bestDistance) = (there, distance);
        }
        return best;
    }
}

// -- The creatures, placed from their prefabs.

/// <summary>A creature, which walks at the player. Its prefab gives it a body, a health and a look.</summary>
[Behavior]
[SceneComponent]
public struct Creature
{
    public float Speed;
    public int Worth;
    public float Flash;

    [OnUpdate]
    [InState(Round.Fighting)]
    public void Chase(BehaviorContext ctx, ref CharacterController controller, in PhysicsBody body)
    {
        var to = Swarm.Flat(ctx.Res<Arena>().PlayerAt) - Swarm.Flat(ctx.Physics.GetPosition(body));
        controller.Velocity = to.LengthSquared() < 0.01f ? Vector3.Zero : Vector3.Normalize(to) * Speed * ctx.Res<Tuning>().EnemySpeed;
    }

    /// <summary>Shows a hit as a flash of light and the wear as a darker body, only when the health changed.</summary>
    [OnUpdate]
    [Changed(typeof(Health))]
    public void Wound(BehaviorContext ctx, in Health health, ref Material material)
    {
        Flash = 1;
        var left = Math.Clamp(health.Value / health.Max, 0, 1);
        material.Albedo = new Vector4(new Vector3(0.25f + 0.75f * left) * health.Tint, 1);
    }

    /// <summary>Fades each flash out, for the creatures flashing.</summary>
    [OnUpdate]
    public void Fade(BehaviorContext ctx, ref Material material)
    {
        if (Flash <= 0) return;
        Flash = MathF.Max(0, Flash - (float)ctx.Time.DeltaSeconds * 6);
        material.EmissiveFactor = new Vector3(Flash * 4, Flash * 3, Flash * 2);
    }
}

/// <summary>How much harm a creature takes before it falls, which shots take away.</summary>
[SceneComponent]
public struct Health
{
    public float Value;
    public float Max;
    public Vector3 Tint;
}

/// <summary>Where the prefabs come from, written by `Swarm --build`.</summary>
public static class Prefabs
{
    public const string Crawler = "resources/prefabs/crawler.json";
    public const string Brute = "resources/prefabs/brute.json";

    public static void Build(string folder)
    {
        SetConfigFlags(ConfigFlags.WindowHidden);
        InitWindow(320, 180, "Swarm prefabs");
        Shapes.Make();
        var ecs = GetApp().World.Resource<EcsWorld>();
        Directory.CreateDirectory(folder);
        Write(ecs, Path.Combine(folder, "crawler.json"), new Vector3(1, 0.35f, 0.3f), size: 0.45f, health: 40, speed: 3.4f, worth: 10);
        Write(ecs, Path.Combine(folder, "brute.json"), new Vector3(0.6f, 0.35f, 1), size: 0.8f, health: 220, speed: 2.1f, worth: 50);
        CloseWindow();
        Console.WriteLine($"Prefabs written to {Path.GetFullPath(folder)}");
    }

    private static void Write(EcsWorld ecs, string file, Vector3 tint, float size, float health, float speed, int worth)
    {
        var creature = ecs.Spawn();
        ecs.SetName(creature, Path.GetFileNameWithoutExtension(file));
        ecs.Add(creature, new Transform(new Vector3(0, size, 0), new Vector3(size)));
        ecs.Add(creature, Shapes.Ball);
        ecs.Add(creature, new Material(new Vector4(tint, 1)) { RoughnessFactor = 0.5f });
        ecs.Add(creature, Collider.Capsule(size, size * 2));
        ecs.Add(creature, RigidBody.Dynamic(20 * size));
        ecs.Add(creature, CharacterController.Default);
        ecs.Add(creature, new Creature { Speed = speed, Worth = worth });
        ecs.Add(creature, new Health { Value = health, Max = health, Tint = tint });
        SaveScene(file, [ecs.Handle(creature)]);
        ecs.Despawn(creature);
    }
}

// -- Shots.

/// <summary>A shot, a trigger flying flat until it hits a creature or its life runs out.</summary>
[Behavior]
public struct Shot
{
    public Vector3 Velocity;
    public float Life;

    /// <summary>Sends a shot on its way the frame its body is made.</summary>
    [OnUpdate]
    [Added(typeof(PhysicsBody))]
    public void Launch(BehaviorContext ctx, in PhysicsBody body) => ctx.Physics.SetLinearVelocity(body, Velocity);

    [OnUpdate]
    public void Age(BehaviorContext ctx)
    {
        Life -= (float)ctx.Time.DeltaSeconds;
        if (Life <= 0) ctx.Cmd.Despawn(ctx.EntityId);
    }
}

// -- The rules over the whole game, each a static method and so run on the main thread.

[Behavior]
public struct Rules
{
    /// <summary>Shots that met a creature harm it by how fast they met, and creatures that reach the player harm the player.</summary>
    [OnUpdate]
    [InState(Screen.Playing)]
    public static void Contacts(BehaviorContext ctx)
    {
        var ecs = ctx.Ecs;
        var arena = ctx.Res<Arena>();
        var tuning = ctx.Res<Tuning>();
        var sounds = ctx.Res<Sounds>();
        foreach (var contact in ctx.World.ReadEvents<ContactStarted>())
        {
            if (!ecs.TryResolve(contact.A, out var a) || !ecs.TryResolve(contact.B, out var b)) continue;
            if (ecs.Has<Shot>(b)) (a, b) = (b, a);
            if (ecs.Has<Shot>(a) && ecs.Has<Health>(b))
            {
                ecs.GetRef<Health>(b).Value -= MathF.Max(contact.Speed, 1) * tuning.DamagePerSpeed;
                ecs.Remove<Shot>(a);
                ctx.Cmd.Despawn(a);
                sounds.Play("hit", 0.8f + Random.Shared.NextSingle() * 0.4f);
                continue;
            }
            if (ecs.Has<Player>(a)) (a, b) = (b, a);
            if (ecs.Has<Creature>(a) && ecs.Has<Player>(b))
            {
                if (!SwarmCommands.Immortal) arena.Health -= ecs.GetReadOnly<Creature>(a).Worth > 20 ? 25 : 10;
                ecs.GetRef<Player>(b).Flash = 1;
                sounds.Play("hurt");
                if (arena.Health <= 0)
                {
                    arena.Best = Math.Max(arena.Best, arena.Score);
                    sounds.Play("over");
                    ctx.SetState(Screen.Over);
                }
            }
        }
    }

    /// <summary>A creature whose health ran out falls, with the copy of its prefab around it.</summary>
    [OnUpdate]
    [InState(Screen.Playing)]
    public static void Fall(BehaviorContext ctx)
    {
        var ecs = ctx.Ecs;
        foreach (var (entity, health) in ecs.Query<Health>())
        {
            if (health.Value > 0) continue;
            var arena = ctx.Res<Arena>();
            arena.Score += ecs.GetReadOnly<Creature>(entity).Worth;
            arena.Kills++;
            ctx.Res<Sounds>().Play("pop", 0.8f + Random.Shared.NextSingle() * 0.5f);
            // The placed copy of the prefab, which goes with the creature under it.
            var copy = ecs.ParentOf(entity);
            ctx.Cmd.DespawnRecursive(copy != 0 ? copy : entity);
        }
    }

    /// <summary>
    /// Ends the wave once its last creature is gone, looking only in a frame after one went, which
    /// the creatures removed since this last ran say.
    /// </summary>
    [OnUpdate]
    [InState(Round.Fighting)]
    public static void Won(BehaviorContext ctx)
    {
        if (ctx.Ecs.Removed<Creature>().Count == 0) return;
        if (ctx.Res<Arena>().ToSpawn == 0 && ctx.Ecs.Count<Creature>() == 0 && ctx.Ecs.Count<SceneRef>() == 0)
            ctx.SetState(Round.Break);
    }

    [OnEnter(Round.Fighting)]
    public static void StartWave(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        var tuning = ctx.Res<Tuning>();
        arena.Wave++;
        arena.ToSpawn = tuning.FirstWave + tuning.MorePerWave * (arena.Wave - 1);
        arena.SpawnTimer = 0;
        ctx.Res<Sounds>().Play("wave");
    }

    /// <summary>A breather heals the player a little before the next wave.</summary>
    [OnTransition(Round.Break, Round.Fighting)]
    public static void Breather(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        arena.Health = MathF.Min(100, arena.Health + 25);
    }

    [OnEnter(Round.Break)]
    public static void StartBreak(BehaviorContext ctx) => ctx.Res<Arena>().BreakTimer = 3;

    [OnUpdate]
    [InState(Round.Break)]
    public static void Rest(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        arena.BreakTimer -= (float)ctx.Time.DeltaSeconds;
        if (arena.BreakTimer <= 0) ctx.SetState(Round.Fighting);
    }

    /// <summary>Places the wave's creatures at the edge of the arena a few at a time, a brute every eighth from the third wave on.</summary>
    [OnUpdate]
    [InState(Round.Fighting)]
    public static void Spawn(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        if (arena.ToSpawn == 0) return;
        arena.SpawnTimer -= (float)ctx.Time.DeltaSeconds;
        if (arena.SpawnTimer > 0) return;
        arena.SpawnTimer = MathF.Max(0.05f, 0.6f - arena.Wave * 0.05f);
        var group = Math.Min(arena.ToSpawn, 2 + arena.Wave);
        var angle = Random.Shared.NextSingle() * MathF.Tau;
        for (int i = 0; i < group; i++)
        {
            var (sin, cos) = MathF.SinCos(angle + (i - group / 2f) * 0.08f);
            var at = new Vector3(cos, 0, sin) * (Swarm.Radius - 1 - i % 3);
            var prefab = arena.Wave >= 3 && (arena.ToSpawn - i) % 8 == 0 ? Prefabs.Brute : Prefabs.Crawler;
            ctx.Cmd.Spawn((entity, ecs) =>
            {
                ecs.Add(entity, new SceneRef { Path = prefab });
                ecs.Add(entity, new Transform(at));
                ecs.DespawnOnExit(entity, Round.Fighting);
            });
        }
        arena.ToSpawn -= group;
    }

    /// <summary>Enter starts a game from the title or after one ends.</summary>
    [OnUpdate]
    public static void Menu(BehaviorContext ctx)
    {
        if (ctx.State<Screen>() != Screen.Playing && (ctx.Input.KeyPressed(Key.Return) || ctx.Input.KeyPressed(Key.Space)))
            ctx.SetState(Screen.Playing);
    }

    [OnTransition(Screen.Over, Screen.Playing)]
    public static void Again(BehaviorContext ctx) => Logger.Info($"Again, after a best of {ctx.Res<Arena>().Best}.");

    private static readonly ILogger Logger = Log.Category("Swarm");
}

// -- The HUD.

[Behavior]
public struct Hud
{
    [OnUpdate]
    public static void Draw(BehaviorContext ctx)
    {
        var arena = ctx.Res<Arena>();
        var screen = ctx.State<Screen>();
        var size = new Vector2(GetScreenWidth(), GetScreenHeight());
        const ImGuiWindowFlags still = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize
                                       | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
        if (screen == Screen.Playing)
        {
            ImGui.SetNextWindowPos(new Vector2(12, 12));
            ImGui.SetNextWindowBgAlpha(0.55f);
            ImGui.Begin("Status", still);
            ImGui.Text($"Wave {arena.Wave}");
            ImGui.Text($"Score {arena.Score}");
            ImGui.Text($"Creatures {ctx.Ecs.Count<Creature>() + arena.ToSpawn}");
            ImGui.Text($"Fallen {arena.Kills}");
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, arena.Health > 30 ? new Vector4(0.3f, 0.8f, 0.4f, 1) : new Vector4(0.9f, 0.3f, 0.2f, 1));
            ImGui.ProgressBar(Math.Max(0, arena.Health) / 100, new Vector2(180, 0), $"{Math.Max(0, arena.Health):0}");
            ImGui.PopStyleColor();
            if (ctx.World.TryGetResource<State<Round>>(out var round) && round.Current == Round.Break)
                ImGui.Text($"Next wave in {Math.Max(0, arena.BreakTimer):0.0}");
            ImGui.End();
            return;
        }

        ImGui.SetNextWindowPos(size / 2, ImGuiCond.Always, new Vector2(0.5f));
        ImGui.SetNextWindowBgAlpha(0.75f);
        ImGui.Begin("Menu", still);
        if (screen == Screen.Title)
        {
            ImGui.Text("SWARM");
            ImGui.Separator();
            ImGui.Text("Hold out against the waves.");
            ImGui.Text("WASD or the arrows move, and the gun fires itself.");
        }
        else
        {
            ImGui.Text("OVERRUN");
            ImGui.Separator();
            ImGui.Text($"Wave {arena.Wave}, score {arena.Score}, best {arena.Best}");
        }
        ImGui.Text("Enter to play");
        ImGui.End();
    }
}

// -- Commands for ./e3d.

public static class SwarmCommands
{
    [Command("swarm.status", "The state, the wave, the score, the player's health and how many creatures and shots there are")]
    internal static string Status()
    {
        var world = GetApp().World;
        var arena = world.Resource<Arena>();
        var ecs = world.Resource<EcsWorld>();
        var round = world.TryGetResource<State<Round>>(out var r) ? r.Current.ToString() : "-";
        return $"{world.Resource<State<Screen>>().Current} {round} wave {arena.Wave} score {arena.Score} health {arena.Health:0} " +
               $"creatures {ecs.Count<Creature>()} waiting {arena.ToSpawn} shots {ecs.Count<Shot>()} fallen {arena.Kills}";
    }

    [Command("swarm.invulnerable", "Keeps the player's health full, for a run that watches the waves: swarm.invulnerable <on>")]
    internal static string Invulnerable(bool on)
    {
        Immortal = on;
        return on ? "the player cannot fall" : "the player can fall";
    }

    internal static bool Immortal;

    [Command("swarm.wave", "Ends the wave being fought and starts wave n after the break: swarm.wave <n>")]
    internal static string Wave(int n)
    {
        var world = GetApp().World;
        var arena = world.Resource<Arena>();
        if (!world.TryGetResource<State<Round>>(out _)) return "not playing";
        arena.Wave = Math.Max(0, n - 1);
        arena.ToSpawn = 0;
        arena.BreakTimer = 0;
        world.Resource<NextState<Round>>().Set(Round.Break);
        return $"wave {n} next";
    }

}
