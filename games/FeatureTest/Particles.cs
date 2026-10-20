using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The particles: the default fountain, a fire that adds its light, smoke laid over by alpha, sparks
/// that bounce off a slab, particles the sun lights, and a burst of a hundred and fifty given off at
/// once every two seconds, each emitter in a row across the zone.
/// </summary>
/// <remarks>
/// Every half second the particles alone are drawn into a texture from a camera in front of the
/// row, as a 3D camera's drawing into a texture draws them, and read back, and the pixels each
/// emitter's stretch of the picture covers are counted, the burst's at its most. The particles are
/// ok when every emitter covers some.
/// </remarks>
sealed class Particles : Zone
{
    private const int Width = 160, Height = 90;
    private readonly Model _slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly RenderTexture2D _probe = LoadRenderTexture(Width, Height);
    private readonly (string Name, float Across, ParticleEmitterHandle Handle)[] _emitters;
    private readonly ParticleEmitterHandle _burst;
    private readonly int[] _covered;
    private readonly Toggle _emitting;
    private float _clock;
    private int _frames, _reads, _burstMost;

    public Particles()
    {
        var fountain = ParticleEmitter.Default;
        var fire = ParticleEmitter.Default with
        {
            Blend = ParticleBlend.Additive, StartColor = new Color(255, 180, 60), EndColor = new Color(200, 40, 10, 0),
            Velocity = new Vector3(0, 2.5f, 0), Spread = 15, Life = 1.2f, Rate = 80, StartSize = 0.35f, EndSize = 0.05f, Gravity = Vector3.Zero, Intensity = 3,
        };
        var smoke = ParticleEmitter.Default with
        {
            Blend = ParticleBlend.Alpha, StartColor = new Color(120, 120, 120, 200), EndColor = new Color(200, 200, 200, 0),
            Velocity = new Vector3(0, 1, 0), Spread = 20, Life = 4, Rate = 30, StartSize = 0.4f, EndSize = 1.5f, Drag = 0.5f, Gravity = Vector3.Zero,
        };
        var sparks = ParticleEmitter.Default with
        {
            Collision = ParticleCollision.Bounce, Bounce = 0.5f, Gravity = new Vector3(0, -9.8f, 0), Velocity = new Vector3(1.5f, 4, 0),
            Spread = 40, StartColor = new Color(255, 230, 120), EndColor = new Color(255, 120, 40), Life = 3, Rate = 40, StartSize = 0.18f, EndSize = 0.1f,
        };
        var lit = ParticleEmitter.Default with { Lit = true, StartColor = new Color(230, 230, 230), EndColor = new Color(230, 230, 230), StartSize = 0.25f };
        var burst = ParticleEmitter.Default with
        {
            Rate = 0, Velocity = new Vector3(0, 1.5f, 0), Spread = 180, Life = 1.5f, Gravity = new Vector3(0, -1, 0),
            StartColor = new Color(120, 200, 255), EndColor = new Color(120, 200, 255, 0), StartSize = 0.25f,
        };
        _emitters =
        [
            ("fountain", -6, CreateParticleEmitter(Place(-6, 0, 0.2f), fountain)),
            ("fire", -3.5f, CreateParticleEmitter(Place(-3.5f, 0, 0.1f), fire)),
            ("smoke", -1, CreateParticleEmitter(Place(-1, 0, 0.5f), smoke)),
            ("sparks", 1.5f, CreateParticleEmitter(Place(1.5f, 0, 2.5f), sparks)),
            ("lit", 4, CreateParticleEmitter(Place(4, 0, 0.2f), lit)),
            ("burst", 6.5f, CreateParticleEmitter(Place(6.5f, 0, 2), burst)),
        ];
        _burst = _emitters[^1].Handle;
        _covered = new int[_emitters.Length];
        _emitting = new Toggle("emitting", "The emitters give off particles", true, on =>
        {
            foreach (var (_, _, handle) in _emitters) SetParticleEmitterActive(handle, on);
        });
    }

    public override string Name => "Particles";
    public override Vector3 Center => Zones.At(135);
    public override Vector3 Start => Place(0, -9);
    public override IReadOnlyList<Toggle> Toggles => [_emitting];

    // From in front of the row, which it spans side to side.
    private Camera3D Front => new(Place(0, -10, 2), Place(0, 0, 2), Vector3.UnitY, 60);

    public override void Update(float dt, Player player)
    {
        _clock += dt;
        if (_emitting.On && _clock >= 2)
        {
            _clock = 0;
            EmitParticles(_burst, 150);
        }

        _frames++;
        if (_frames < 30 || _frames % 30 != 1) return;
        var image = LoadImageFromTexture(_probe.Texture);
        if (!IsImageValid(image)) return;
        Array.Clear(_covered);
        for (var i = 0; i < _emitters.Length; i++)
        {
            // The stretch of the picture a meter and a quarter either side of the emitter.
            var left = (int)GetWorldToScreenEx(Place(_emitters[i].Across - 1.25f, 0, 2), Front, Width, Height).X;
            var right = (int)GetWorldToScreenEx(Place(_emitters[i].Across + 1.25f, 0, 2), Front, Width, Height).X;
            for (var y = 0; y < Height; y++)
                for (var x = Math.Max(0, Math.Min(left, right)); x < Math.Min(Width, Math.Max(left, right)); x++)
                {
                    var c = GetImageColor(image, x, y);
                    if (Math.Max(c.R, Math.Max(c.G, c.B)) > 30) _covered[i]++;
                }
        }
        UnloadImage(image);
        _reads++;
        _burstMost = Math.Max(_burstMost, _covered[^1]);
    }

    public override void DrawOffscreen()
    {
        if (_frames % 30 != 0) return;
        BeginTextureMode(_probe);
        ClearBackground(Color.Black);
        // The particles are drawn through the camera of a texture's 3D mode with nothing else.
        BeginMode3D(Front);
        EndMode3D();
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        // The slab the sparks bounce off, under them.
        Box(_slab, Place(2, 0, 0.25f), new Vector3(3, 0.5f, 3), new Color(150, 150, 160));
    }

    public override string Status()
    {
        if (_reads == 0) return "the particles not read yet";
        var counts = _emitters.Select((e, i) => $"the {e.Name} {(i == _emitters.Length - 1 ? _burstMost : _covered[i])}");
        var ok = _covered[..^1].All(c => c > 5) && _burstMost > 5;
        return $"{string.Join(", ", counts)} pixels in {_reads} reads{(ok ? ", particles ok" : "")}{(_emitting.On ? "" : ", the emitters stopped")}";
    }
}
