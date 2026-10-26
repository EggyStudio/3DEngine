using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The lights and shadows, a red lamp casting shadows round a pillar, a blue spot turning slowly
/// over the floor, a block the sun throws a shadow from, an emissive sphere, spheres from rough to
/// polished and from dielectric to metal, and a room a reflection probe lights with itself.
/// </summary>
/// <remarks>
/// Each second while the player is in the zone, the zone is drawn into a texture from a camera
/// high over it and read back, and the floor's colour read where each light falls, under the lamp
/// beside the open floor, under the spot, in the block's shadow beside the floor next to it, and on
/// the emissive sphere. The sun's shadows are drawn round the camera the player looks through, so
/// the read waits for the player to be here. The lights are ok when each of those reads as its
/// light makes it.
/// </remarks>
sealed class Lights : Zone
{
    private const int Probe = 128;
    // Grey, so the sun leaves room above it for a lamp's colour.
    private readonly Model _floor = Lit(GenMeshPlane(26, 22, 1, 1), new Color(110, 110, 110));
    private readonly Model _pillar = Lit(GenMeshCube(0.5f, 2, 0.5f), new Color(200, 200, 200));
    private readonly Model _block = Lit(GenMeshCube(1.2f, 3, 1.2f), new Color(200, 200, 200));
    private readonly Model _glow = LoadModelFromMesh(GenMeshSphere(0.5f, 16, 24));
    // Six balls from rough to polished, every other one metal, and a polished metal one in the room.
    private readonly Model[] _balls = [.. Enumerable.Range(0, 7).Select(i => Lit(GenMeshSphere(0.45f, 16, 24),
        i < 6 ? new Color(200, 170, 120) : new Color(230, 230, 230), i < 6 ? i / 5f : 0.05f, i < 6 ? i % 2 : 1))];
    private readonly Model _wall = Lit(GenMeshCube(1, 1, 1), new Color(220, 200, 170));
    private readonly LightHandle _lamp, _spot;
    private readonly ReflectionProbeHandle _room;
    private readonly RenderTexture2D _probe = LoadRenderTexture(Probe, Probe);
    private readonly Toggle _lit = new("lamps", "The lamp and the spot");
    private readonly Toggle _shading;
    private float _time;
    private int _frames, _reads;
    private Color _red, _open, _blue, _shade, _bright;

    public Lights()
    {
        _glow.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 200, 80), EmissiveIntensity = 4, CastsShadows = false };
        _lamp = CreatePointLight(Center + new Vector3(-8, 2.2f, -2), new Color(255, 60, 40), 30, range: 7, castsShadows: true);
        _spot = CreateSpotLight(Center + new Vector3(0, 5, -4), -Vector3.UnitY, new Color(60, 110, 255), 60, 20, 30, range: 10, castsShadows: true);
        _room = CreateReflectionProbe(Center + new Vector3(-8, 1.5f, 7), new Vector3(4, 3, 4));
        _shading = new Toggle("shadows", "The lamp and the spot cast shadows", true, on =>
        {
            SetLightCastsShadows(_lamp, on);
            SetLightCastsShadows(_spot, on);
        });
    }

    public override string Name => "Lights";
    public override Vector3 Center => Zones.At(0);
    public override Vector3 Start => Center + new Vector3(0, 0, 14);
    public override IReadOnlyList<Toggle> Toggles => [_lit, _shading];

    private static Model Lit(ModelMesh mesh, Color color, float roughness = 0.5f, float metallic = 0)
    {
        var model = LoadModelFromMesh(mesh);
        model.Materials[0] = new ModelMaterial(color) { Roughness = roughness, Metallic = metallic };
        return model;
    }

    // The camera the zone is read through, high over its middle looking down.
    private Camera3D Above => new(Center + new Vector3(0, 22, 0.01f), Center, -Vector3.UnitZ, 60);

    public override void Update(float dt, Player player)
    {
        _time += dt;
        SetLightColor(_lamp, new Color(255, 60, 40), _lit.On ? 30 : 0);
        SetLightColor(_spot, new Color(60, 110, 255), _lit.On ? 60 : 0);
        SetLightDirection(_spot, Vector3.Normalize(new Vector3(0.25f * MathF.Sin(_time * 0.5f), -1, 0.25f * MathF.Cos(_time * 0.5f))));

        _frames = Zones.Around(player.Feet) == this ? _frames + 1 : 0;
        if (_frames < 60 || _frames % 60 != 1) return;
        var image = LoadImageFromTexture(_probe.Texture);
        if (!IsImageValid(image)) return;
        Color Read(Vector3 offset)
        {
            var at = GetWorldToScreenEx(Center + offset, Above, Probe, Probe);
            int r = 0, g = 0, b = 0;
            for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++)
                {
                    var c = GetImageColor(image, Math.Clamp((int)at.X + x, 0, Probe - 1), Math.Clamp((int)at.Y + y, 0, Probe - 1));
                    (r, g, b) = (r + c.R, g + c.G, b + c.B);
                }
            return new Color((byte)(r / 9), (byte)(g / 9), (byte)(b / 9));
        }
        _red = Read(new Vector3(-8, 0, -4));
        _blue = Read(new Vector3(0, 0, -4));
        // The sun comes from behind and to the right as the hub looks north, so the block's shadow
        // falls toward the hub's side of it and to the left.
        _shade = Read(new Vector3(6, 0, -0.85f));
        _open = Read(new Vector3(6, 0, 2.5f));
        _bright = Read(new Vector3(-3, 1, 4));
        UnloadImage(image);
        _reads++;
    }

    public override void DrawOffscreen()
    {
        if (_frames < 59 || _frames % 60 != 0) return;
        BeginTextureMode(_probe);
        ClearBackground(Color.Black);
        BeginMode3D(Above);
        Scene();
        EndMode3D();
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        Scene();
        if (!_lit.On) return;
        DrawSphere(Center + new Vector3(-8, 2.2f, -2), 0.12f, new Color(255, 90, 70));
        DrawCylinder(Center + new Vector3(0, 5, -4), 0.05f, 0.25f, 0.3f, 12, new Color(90, 140, 255));
    }

    // What both the window and the probe draw.
    private void Scene()
    {
        DrawModel(_floor, Center + new Vector3(0, 0.02f, 0), 1, Color.White);
        DrawModel(_pillar, Center + new Vector3(-8, 1, 0), 1, Color.White);
        DrawModel(_block, Center + new Vector3(7, 1.5f, 0), 1, Color.White);
        DrawModel(_glow, Center + new Vector3(-3, 1, 4), 1, Color.White);
        for (var i = 0; i < 6; i++) DrawModel(_balls[i], Center + new Vector3(-1 + i * 1.2f, 0.5f, 7), 1, Color.White);
        // A room open toward the hub, its probe catching its walls, and a polished ball in it.
        foreach (var (at, size) in new[] { (new Vector3(-8, 1.5f, 5), new Vector3(4, 3, 0.2f)), (new Vector3(-10, 1.5f, 7), new Vector3(0.2f, 3, 4)), (new Vector3(-6, 1.5f, 7), new Vector3(0.2f, 3, 4)) })
        {
            _wall.Transform = Matrix4x4.CreateScale(size);
            DrawModel(_wall, Center + at, 1, Color.White);
        }
        DrawModel(_balls[6], Center + new Vector3(-8, 0.5f, 7), 1, Color.White);
    }

    private static string Text(Color c) => $"{c.R},{c.G},{c.B}";
    private static int Light(Color c) => c.R + c.G + c.B;

    public override string Status()
    {
        var room = IsReflectionProbeReady(_room) ? "the room's reflection probe ready" : "the room's reflection probe not ready";
        if (_reads == 0) return $"the floor not read yet, which waits for you to be here, {room}";
        var red = _red.R - _red.G > 25 && _open.R - _open.G < 15;
        var blue = _blue.B - _blue.R > 25;
        var shaded = Light(_open) - Light(_shade) > 45;
        var glows = _bright.R > 200;
        var ok = red && blue && shaded && glows;
        return $"the lamp's patch {Text(_red)} beside the open floor's {Text(_open)}, the spot's {Text(_blue)}, "
               + $"the block's shadow {Text(_shade)}, the glowing sphere {Text(_bright)}, {room}, {_reads} reads"
               + (ok ? ", lights ok" : $"{(red ? "" : ", no red from the lamp")}{(blue ? "" : ", no blue from the spot")}{(shaded ? "" : ", no shadow from the block")}{(glows ? "" : ", no glow")}")
               + (_lit.On ? "" : ", the lamps off");
    }
}
