using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The effects over the frame and the shaders of the program's own: a board with a red patch and a
/// glowing bar that bloom, a vignette, colour grading, exposure, depth of field, motion blur and a
/// tonemapping curve are shown on in turn while the player is in the zone, a knot drawn through a
/// toon shader, a compute shader stepping the Game of Life in a buffer, another painting a texture,
/// and a 2D shader drawing into a texture through a greying shader.
/// </summary>
/// <remarks>
/// <para>
/// Arriving, each effect is turned on alone for a second, the first second none, and the window
/// read at its end where the bar's glow spreads, at the window's left edge, on the red patch and on
/// the grey board, as the camera sees them from the zone's start. Bloom lifts the glow beside the
/// bar, the vignette darkens the edge, grading with no saturation greys the patch and exposure
/// brightens the board, each against the second with none. Every effect is turned off again after
/// the last, and as the player leaves.
/// </para>
/// <para>
/// The shaders are read back wherever the player is: the Game of Life's buffer read after each step
/// for a blinker of three cells that turns from row to column and back, the painted texture for the
/// colours in it, and the greyed texture for a red drawn grey.
/// </para>
/// </remarks>
sealed class Effects : Zone
{
    private const int Phase = 60, Side = 16;
    private static readonly string[] Phases = ["none", "bloom", "vignette", "grading", "exposure", "depth of field", "motion blur", "tonemap"];
    private readonly Model _cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly Model _bar = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly Model _knot = LoadModelFromMesh(GenMeshKnot(0.6f, 0.5f, 24, 96));
    private readonly Shader _toon = LoadShader("resources/shaders/toon.slang");
    private readonly Shader _life = LoadComputeShader("resources/shaders/life.slang");
    private readonly Shader _plasma = LoadComputeShader("resources/shaders/plasma.slang");
    private readonly Shader _grey = LoadShader("resources/shaders/grayscale.slang");
    private readonly ShaderBuffer[] _grids;
    private readonly Texture2D _painted = LoadTextureFromImage(GenImageColor(64, 64, Color.Black));
    private readonly RenderTexture2D _greyed = LoadRenderTexture(16, 16);
    private readonly Tonemap _curve = GetTonemap();
    private readonly Dictionary<string, (Color Glow, Color Edge, Color Red, Color Grey)> _seen = [];
    private readonly uint[] _cells = new uint[Side * Side];
    private int _frames, _all, _generation, _turns;
    private float _time;
    private bool _row = true, _lifeOk = true;
    private string _shaders = "the shaders not read yet";
    private int _colours;
    private Color _redGreyed;

    public Effects()
    {
        _bar.Materials[0] = new ModelMaterial(Color.White) { Emissive = new Color(255, 245, 200), EmissiveIntensity = 8, CastsShadows = false };
        _knot.Materials[0].Shader = _toon;
        SetShaderValue(_grey, 0, 1f);
        // A blinker of three cells in a row across the middle of a grid that wraps round.
        var start = new uint[Side * Side];
        for (var x = 7; x <= 9; x++) start[8 * Side + x] = 1;
        _grids = [LoadShaderBuffer<uint>(start), LoadShaderBuffer(Side * Side * 4)];
        SetShaderValue(_life, GetShaderLocation(_life, "width"), Side);
        SetShaderValue(_life, GetShaderLocation(_life, "height"), Side);
    }

    public override string Name => "Effects";
    public override Vector3 Center => Zones.At(180);
    public override Vector3 Start => Place(0, -8);

    public override void Arrive(string? spot) => _frames = 0;

    public override void Update(float dt, Player player)
    {
        _time += dt;
        _all++;
        Shaders();

        var here = Zones.Around(player.Feet) == this;
        if (!here)
        {
            if (_frames > 0 && _frames < Phases.Length * Phase) Reset();
            _frames = 0;
            return;
        }
        if (_frames == Phases.Length * Phase) return;
        if (_frames == 0) Pixels.Keep();
        if (_frames % Phase == 0) Show(Phases[_frames / Phase]);
        _frames++;
        if (_frames % Phase != 0) return;

        // The window as the phase's last frame showed it, where the camera sees each place.
        var image = LoadImageFromScreen();
        if (IsImageValid(image))
        {
            var camera = player.Camera;
            _seen[Phases[_frames / Phase - 1]] = (
                Pixels.Average(image, GetWorldToScreen(Place(1.4f, 2.84f, 2.6f), camera)),
                Pixels.Average(image, new Vector2(6, image.Height / 2)),
                Pixels.Average(image, GetWorldToScreen(Place(-2.5f, 2.78f, 2), camera)),
                Pixels.Average(image, GetWorldToScreen(Place(3, 2.84f, 2.4f), camera)));
            UnloadImage(image);
        }
        if (_frames == Phases.Length * Phase) Reset();
    }

    private void Show(string effect)
    {
        Reset();
        switch (effect)
        {
            case "bloom": SetBloom(2, 0.8f); break;
            case "vignette": SetVignette(0.9f, 0.35f); break;
            case "grading": SetColorGrading(1, 0, Color.White); break;
            case "exposure": SetExposure(2); break;
            case "depth of field": SetDepthOfField(4, 1, 6); break;
            case "motion blur": SetMotionBlur(0.8f); break;
            case "tonemap": SetTonemap(Tonemap.AgX); break;
        }
    }

    private void Reset()
    {
        SetBloom(0);
        SetVignette(0);
        SetColorGrading(1, 1, Color.White);
        SetExposure(1);
        SetDepthOfField(0, 0, 0);
        SetMotionBlur(0);
        SetTonemap(_curve);
    }

    // The compute shaders dispatched before the frame, and each read back as it is due.
    private void Shaders()
    {
        if (_all == 2) SetShaderValueTexture(_plasma, GetShaderLocation(_plasma, "image"), _painted);
        if (_all >= 2)
        {
            SetShaderValue(_plasma, GetShaderLocation(_plasma, "time"), _time);
            ComputeShaderDispatch(_plasma, 8, 8, 1);
        }

        // The blinker read after each step, a row and then a column of three by turns, and stepped.
        if (_all % 30 != 0) return;
        ReadShaderBuffer<uint>(_grids[_generation % 2], _cells);
        var alive = _cells.Sum(c => (int)c);
        var row = _cells[8 * Side + 7] == 1 && _cells[8 * Side + 9] == 1;
        var column = _cells[7 * Side + 8] == 1 && _cells[9 * Side + 8] == 1;
        if (alive != 3 || row == column || row != _row) _lifeOk = false;
        else if (_generation > 0) _turns++;
        _row = !row;
        SetShaderValueBuffer(_life, GetShaderLocation(_life, "current"), _grids[_generation % 2]);
        SetShaderValueBuffer(_life, GetShaderLocation(_life, "next"), _grids[(_generation + 1) % 2]);
        ComputeShaderDispatch(_life, 1, 1, 1);
        _generation++;

        if (_all % 60 != 0) return;
        var painted = LoadImageFromTexture(_painted);
        if (IsImageValid(painted))
        {
            var colours = new HashSet<int>();
            for (var y = 0; y < 64; y += 2)
                for (var x = 0; x < 64; x += 2)
                {
                    var c = GetImageColor(painted, x, y);
                    colours.Add((c.R >> 4) << 8 | (c.G >> 4) << 4 | c.B >> 4);
                }
            _colours = colours.Count;
            UnloadImage(painted);
        }
        var greyed = LoadImageFromTexture(_greyed.Texture);
        if (IsImageValid(greyed))
        {
            _redGreyed = GetImageColor(greyed, 8, 8);
            UnloadImage(greyed);
        }
        var grey = Math.Abs(_redGreyed.R - _redGreyed.G) < 10 && Math.Abs(_redGreyed.G - _redGreyed.B) < 10 && _redGreyed.R > 40;
        var ok = _lifeOk && _turns >= 2 && _colours > 20 && grey;
        _shaders = $"the Game of Life's blinker turned {_turns} times{(_lifeOk ? " as three cells" : " out of its shape")}, the painted texture of {_colours} colours, "
                   + $"red drawn through the greying shader as {Pixels.Text(_redGreyed)}{(ok ? ", shaders ok" : "")}";
    }

    public override void DrawOffscreen()
    {
        if (_all % 60 != 59) return;
        BeginTextureMode(_greyed);
        ClearBackground(Color.Black);
        BeginShaderMode(_grey);
        DrawRectangle(0, 0, 16, 16, new Color(220, 40, 40));
        EndShaderMode();
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        Box(_cube, Place(0, 3, 2), new Vector3(8, 4, 0.3f), new Color(130, 130, 130));
        Box(_cube, Place(-2.5f, 2.8f, 2), new Vector3(1.2f, 1.2f, 0.1f), new Color(210, 30, 30));
        Box(_bar, Place(1, 2.8f, 2.6f), new Vector3(0.3f, 0.1f, 2), Color.White);
        SetShaderValue(_toon, GetShaderLocation(_toon, "bands"), 4f);
        SetShaderValue(_toon, GetShaderLocation(_toon, "rimColor"), new Vector4(1, 0.85f, 0.4f, 0.9f));
        SetShaderValue(_toon, GetShaderLocation(_toon, "viewer"), camera.Position);
        DrawModel(_knot, Place(-4.5f, -2, 1.3f), 1, new Color(90, 160, 220));
        DrawBillboard(camera, _painted, Place(4.5f, -2, 1.4f), 2, Color.White);
    }

    public override void DrawScreen(Camera3D camera)
    {
        if (_frames > 0 && _frames < Phases.Length * Phase)
            DrawText($"Shown now: {Phases[_frames / Phase]}", 16, GetScreenHeight() - 70, 20, Color.White);
    }

    public override string Status()
    {
        string effects;
        if (!Phases.All(_seen.ContainsKey)) effects = _frames > 0 ? $"showing {Phases[Math.Min(_frames / Phase, Phases.Length - 1)]}" : "the effects not read yet, which waits for you to be here";
        else
        {
            var none = _seen["none"];
            var bloom = Pixels.Light(_seen["bloom"].Glow) - Pixels.Light(none.Glow) > 15;
            var vignette = Pixels.Light(none.Edge) - Pixels.Light(_seen["vignette"].Edge) > 15;
            var grading = none.Red.R - none.Red.G > 25 && Math.Abs(_seen["grading"].Red.R - _seen["grading"].Red.G) < 10;
            var exposure = Pixels.Light(_seen["exposure"].Grey) - Pixels.Light(none.Grey) > 30;
            effects = $"the bar's glow {Pixels.Text(_seen["bloom"].Glow)} with bloom and {Pixels.Text(none.Glow)} without, "
                      + $"the window's edge {Pixels.Text(_seen["vignette"].Edge)} with the vignette and {Pixels.Text(none.Edge)} without, "
                      + $"the red patch {Pixels.Text(none.Red)} graded to {Pixels.Text(_seen["grading"].Red)}, "
                      + $"the board {Pixels.Text(none.Grey)} exposed to {Pixels.Text(_seen["exposure"].Grey)}, depth of field, motion blur and the AgX curve shown"
                      + (bloom && vignette && grading && exposure ? ", effects ok"
                          : $"{(bloom ? "" : ", no bloom")}{(vignette ? "" : ", no vignette")}{(grading ? "" : ", no grading")}{(exposure ? "" : ", no exposure")}");
        }
        return $"{effects}, {_shaders}";
    }
}
