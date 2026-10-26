using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The models and animation, a skinned arm posed by its clip's time, the robot played by an
/// animation state machine from a file it names by id, a strip of morph targets played by its clip
/// and another moved by a weight set by name, four hundred cubes in one instanced draw, every mesh
/// the engine generates on a plinth of its own, a heightmap and a maze from images, and an OBJ with
/// its material file.
/// </summary>
/// <remarks>
/// Every half second the arm is drawn alone into a small texture from a camera of its own and read
/// back, and the pixels its outline covers are counted, so the status says the pose reached the
/// picture by how far that count moved as the arm bends. The robot's machine is written to
/// <c>robot.machine.json</c> beside the program at each start, keeping the id the file was given
/// at the first, and is run through <see cref="AnimationMachine.File"/>, so the states it passes
/// through say the machine, the data file and the robot's clips all work.
/// </remarks>
sealed class Models : Zone
{
    private const int Probe = 96, Instances = 400;
    private readonly Model _arm = LoadModel("resources/arm.gltf");
    private readonly ModelAnimation _bend;
    private readonly Model _strip = LoadModel("resources/morph.gltf"), _weighted = LoadModel("resources/morph.gltf");
    private readonly ModelAnimation _pulse;
    private readonly Model _torus = LoadModel("resources/torus.obj");
    private readonly (string Name, Model Model)[] _made;
    private readonly ModelMesh _cube = GenMeshCube(0.35f, 0.35f, 0.35f);
    private readonly Model _plinth = LoadModelFromMesh(GenMeshCube(1.2f, 0.8f, 1.2f));
    private readonly ModelMaterial _tile = new(new Color(90, 160, 220));
    private readonly Matrix4x4[] _grid = new Matrix4x4[Instances];
    private readonly RenderTexture2D _probe = LoadRenderTexture(Probe, Probe);
    private readonly Toggle _playing = new("playing", "The clips play");
    private readonly Toggle _instanced = new("instanced", "The instanced cubes");
    private readonly EcsWorld _ecs = GetApp().World.Resource<EcsWorld>();
    private readonly int _robot;
    private readonly string _machine;
    private readonly HashSet<string> _states = [];
    private float _time;
    private int _frames, _reads, _least = int.MaxValue, _most = -1;

    public Models()
    {
        _bend = LoadModelAnimations("resources/arm.gltf")[0];
        _pulse = LoadModelAnimations("resources/morph.gltf")[0];
        var noise = GenImagePerlinNoise(32, 32, 0, 0, 4);
        var maze = GenImageChecked(8, 8, 2, 2, Color.White, Color.Black);
        _made =
        [
            ("cube", LoadModelFromMesh(GenMeshCube(0.8f, 0.8f, 0.8f))),
            ("sphere", LoadModelFromMesh(GenMeshSphere(0.45f, 16, 24))),
            ("half sphere", LoadModelFromMesh(GenMeshHemiSphere(0.45f, 8, 24))),
            ("cylinder", LoadModelFromMesh(GenMeshCylinder(0.35f, 0.9f, 24))),
            ("cone", LoadModelFromMesh(GenMeshCone(0.45f, 0.9f, 24))),
            ("torus", LoadModelFromMesh(GenMeshTorus(0.3f, 0.9f, 24, 16))),
            ("knot", LoadModelFromMesh(GenMeshKnot(0.3f, 0.9f, 64, 16))),
            ("polygon", LoadModelFromMesh(GenMeshPoly(6, 0.45f))),
            ("plane", LoadModelFromMesh(GenMeshPlane(0.9f, 0.9f, 4, 4))),
            ("heightmap", LoadModelFromMesh(GenMeshHeightmap(noise, new Vector3(1, 0.4f, 1)))),
            ("maze", LoadModelFromMesh(GenMeshCubicmap(maze, new Vector3(0.12f, 0.2f, 0.12f)))),
        ];
        UnloadImage(noise);
        UnloadImage(maze);

        // Idle below a walking pace, walking up to a run, and running past it.
        var machine = new AnimationStateMachine
        {
            States = [new() { Name = "Idle", Clip = "Idle" }, new() { Name = "Walk", Clip = "Walking" }, new() { Name = "Run", Clip = "Running" }],
            Transitions =
            [
                new() { From = "Idle", To = "Walk", When = AnimationCondition.Above, Parameter = "speed", Value = 0.5f, Fade = 0.25f },
                new() { From = "Walk", To = "Idle", When = AnimationCondition.Below, Parameter = "speed", Value = 0.5f, Fade = 0.25f },
                new() { From = "Walk", To = "Run", When = AnimationCondition.Above, Parameter = "speed", Value = 2, Fade = 0.25f },
                new() { From = "Run", To = "Walk", When = AnimationCondition.Below, Parameter = "speed", Value = 2, Fade = 0.25f },
            ],
        };
        var file = DataAssets.Save("robot.machine.json", machine);
        _machine = $"{Path.GetFileName(file.Path)} ({file.Id})";
        _robot = _ecs.Spawn();
        // At half its size, as Tumble draws it.
        _ecs.Add(_robot, new Transform(At(0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(180 - Facing)), new Vector3(0.5f)));
        _ecs.Add(_robot, new AnimatedModel("resources/robot.glb"));
        _ecs.Add(_robot, new AnimationMachine(file));
    }

    public override string Name => "Models";
    public override Vector3 Center => Zones.At(315);
    public override Vector3 Start => Center - Ahead(315) * 14;
    public override IReadOnlyList<Toggle> Toggles => [_playing, _instanced];

    // A point of the zone, across it to the right and along it away from the hub, and up.
    private Vector3 At(float across, float along, float up = 0)
    {
        var ahead = Ahead(315);
        return Center + new Vector3(-ahead.Z, 0, ahead.X) * across + ahead * along + Vector3.UnitY * up;
    }

    public override void Update(float dt, Player player)
    {
        if (_playing.On) _time += dt;
        UpdateModelAnimationAt(_arm, _bend, _time);
        UpdateModelAnimationAt(_strip, _pulse, _time);
        SetModelMorphWeight(_weighted, "Raise", 0.5f + 0.5f * MathF.Sin(_time * 2));
        _ecs.GetRef<AnimationMachine>(_robot).Set("speed", 1.5f - 1.5f * MathF.Cos(_time * 0.8f));
        if (_ecs.GetReadOnly<AnimationMachine>(_robot).State is { Length: > 0 } state) _states.Add(state);

        for (var i = 0; i < Instances; i++)
        {
            var (row, column) = (i / 20, i % 20);
            var turn = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, 1, (i % 7) - 3)), _time + i * 0.1f);
            _grid[i] = turn * Matrix4x4.CreateTranslation(At(-4.75f + column * 0.5f, 5 + row * 0.5f, 0.6f + 0.2f * MathF.Sin(_time + i)));
        }

        // The probe is drawn on the frame before it is read, every half second.
        _frames++;
        if (_frames < 30 || _frames % 30 != 1) return;
        var image = LoadImageFromTexture(_probe.Texture);
        if (!IsImageValid(image)) return;
        var covered = 0;
        for (var y = 0; y < Probe; y++)
            for (var x = 0; x < Probe; x++)
            {
                var c = GetImageColor(image, x, y);
                if (Math.Max(c.R, Math.Max(c.G, c.B)) > 40) covered++;
            }
        UnloadImage(image);
        if (covered == 0) return;
        _reads++;
        _least = Math.Min(_least, covered);
        _most = Math.Max(_most, covered);
    }

    public override void DrawOffscreen()
    {
        if (_frames % 30 != 0) return;
        BeginTextureMode(_probe);
        ClearBackground(Color.Black);
        // From the arm's side, so its bend changes the outline the most.
        BeginMode3D(new Camera3D(new Vector3(0, 1, 4.5f), new Vector3(0, 1, 0), Vector3.UnitY, 40));
        DrawModel(_arm, Vector3.Zero, 1, Color.White);
        EndMode3D();
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        for (var i = 0; i < _made.Length; i++)
        {
            var plinth = At(-8 + i * 1.6f, -6);
            DrawModelEx(_plinth, plinth + new Vector3(0, 0.4f, 0), Vector3.UnitY, -315, Vector3.One, new Color(170, 165, 155));
            DrawModel(_made[i].Model, plinth + new Vector3(0, 1.25f, 0), 1, ColorFromHSV(i * 32, 0.5f, 0.9f));
        }
        DrawModel(_arm, At(-4, 0), 1, new Color(230, 160, 60));
        DrawModel(_strip, At(4, 0, 0.5f), 1, new Color(120, 200, 140));
        DrawModel(_weighted, At(5.5f, 0, 0.5f), 1, new Color(200, 120, 180));
        DrawModel(_torus, At(8, 0, 1), 0.5f, Color.White);
        if (_instanced.On) DrawMeshInstanced(_cube, _tile, _grid);
    }

    public override string Status()
    {
        var vertices = _made.Sum(made => made.Model.Meshes.Sum(mesh => mesh.VertexCount));
        var arm = _reads < 3 ? "the arm's outline not read yet"
            : $"the arm's outline covering {_least} to {_most} pixels in {_reads} reads";
        var bends = _reads >= 3 && _most - _least >= _most / 10;
        var states = string.Join(", ", new[] { "Idle", "Walk", "Run" }.Where(_states.Contains));
        var ran = _states.Count == 3;
        return $"{arm}{(bends ? ", the arm bends" : "")}, the robot's machine from {_machine} through {(states.Length > 0 ? states : "no state yet")}"
               + $"{(ran ? ", the machine runs" : "")}, {_made.Length} generated meshes of {vertices:N0} vertices, "
               + $"{(_instanced.On ? $"{Instances} cubes in one instanced draw" : "the instanced cubes off")}, the OBJ of {_torus.Meshes.Length} mesh{(_torus.Meshes.Length == 1 ? "" : "es")}"
               + $"{(!_playing.On ? ", the clips held still" : "")}";
    }
}
