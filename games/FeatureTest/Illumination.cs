using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The global illumination, a white room open toward the hub with a red wall and a green one, lit
/// by a lamp and an emissive panel under its ceiling, where global illumination is turned on while
/// the player is in the zone and off again as the player leaves, since it costs every frame.
/// </summary>
/// <remarks>
/// Arriving, the zone reads the window with global illumination off and then, five seconds after
/// turning it on at the low quality, again, each read at four places along the white floor beside
/// the red wall and four beside the green one, as the player's camera sees them from the zone's
/// start. Turned on, it takes the sky's blue out of both, so the light bounces when the floor beside
/// the red wall grows redder, by its red against its green and blue together, than the floor beside
/// the green wall does, which a red the wall sends on alone makes it. Seen from elsewhere the reads
/// say what that view gives instead.
/// </remarks>
sealed class Illumination : Zone
{
    private const int Off = 120, On = 420;
    // The walls' colours are their materials', which global illumination takes the light they send
    // on from, where a tint given as a model is drawn is not.
    private readonly Model _white = Painted(new Color(220, 220, 220)), _red = Painted(new Color(210, 35, 35)), _green = Painted(new Color(40, 210, 50));
    private readonly Model _panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly Toggle _traced = new("gi", "Global illumination in the room");
    private int _frames;
    private bool _lit;
    private Color _redOff, _greenOff, _redOn, _greenOn;
    private string _read = "not read yet, which waits for you to be here";

    public Illumination()
    {
        _panel.Materials[0] = new ModelMaterial(Color.White) { Emissive = new Color(255, 255, 255), EmissiveIntensity = 6, CastsShadows = false };
        // White, so the walls' colours are what tints the floor, and toward the green wall, which the
        // sun reaching in through the opening leaves in shade where it lights the red one.
        CreatePointLight(Place(2, 0.5f, 3.2f), Color.White, 12, range: 8);
    }

    private static Model Painted(Color color)
    {
        var model = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        model.Materials[0] = new ModelMaterial(color);
        return model;
    }

    public override string Name => "Illumination";
    public override Vector3 Center => Zones.At(45);
    public override Vector3 Start => Place(0, -9);
    public override IReadOnlyList<Toggle> Toggles => [_traced];

    public override void Arrive(string? spot) => _frames = 0;

    public override void Update(float dt, Player player)
    {
        var here = Zones.Around(player.Feet) == this && _traced.On;
        _frames = here ? _frames + 1 : 0;
        var wanted = here && _frames > Off;
        if (wanted != _lit)
        {
            SetGlobalIllumination(wanted ? GlobalIllumination.Low : GlobalIllumination.Off);
            // The field global illumination built is let go with it.
            if (!wanted) SetSceneField(0);
            _lit = wanted;
        }
        if (_frames == 1) Pixels.Keep();
        if (_frames != Off && _frames != On) return;

        var image = LoadImageFromScreen();
        if (!IsImageValid(image)) return;
        var camera = player.Camera;
        // Four places along each wall, the light averaged along it, since the low quality reads the
        // cascades' probes alone, two units apart.
        Color Along(float across) => Pixels.Mean([.. new[] { 0f, 0.7f, 1.4f, 2.1f }.Select(along => Pixels.Average(image, GetWorldToScreen(Place(across, along, 0.11f), camera)))]);
        var red = Along(-2.8f);
        var green = Along(2.8f);
        UnloadImage(image);
        if (_frames == Off) (_redOff, _greenOff) = (red, green);
        else
        {
            (_redOn, _greenOn) = (red, green);
            static float Share(Color c) => c.R / (float)Math.Max(1, c.G + c.B);
            var redder = Share(_redOn) - Share(_redOff) - (Share(_greenOn) - Share(_greenOff));
            var bounces = redder > 0.03f;
            _read = $"the floor by the red wall {Pixels.Text(_redOff)} with it off and {Pixels.Text(_redOn)} with it on, "
                    + $"by the green wall {Pixels.Text(_greenOff)} and {Pixels.Text(_greenOn)}, the red wall's side {redder:0.000} redder"
                    + (bounces ? ", the light bounces" : ", no red taken from the wall");
        }
    }

    public override void Draw(Camera3D camera)
    {
        Box(_white, Place(0, 0.5f, 0.05f), new Vector3(6.4f, 0.1f, 5.2f), Color.White);
        Box(_white, Place(0, 0.5f, 4.05f), new Vector3(6.4f, 0.1f, 5.2f), Color.White);
        Box(_red, Place(-3.1f, 0.5f, 2), new Vector3(0.2f, 4.2f, 5.2f), Color.White);
        Box(_green, Place(3.1f, 0.5f, 2), new Vector3(0.2f, 4.2f, 5.2f), Color.White);
        Box(_white, Place(0, 3.1f, 2), new Vector3(6.4f, 4.2f, 0.2f), Color.White);
        // A lintel over the opening, which keeps the high sun off the walls and leaves the floor in view.
        Box(_white, Place(0, -2.1f, 3.25f), new Vector3(6.4f, 1.5f, 0.2f), Color.White);
        Box(_white, Place(0, 0.8f, 1), new Vector3(1.2f, 2, 1.2f), Color.White);
        Box(_panel, Place(0, 0.5f, 3.97f), new Vector3(1.6f, 0.06f, 1.6f), Color.White);
    }

    public override string Status() =>
        $"global illumination {(_lit ? "on at the low quality" : "off")}, {_read}{(_traced.On ? "" : ", turned off for the room")}";
}

/// <summary>Colours read back from an image, a few pixels at a time.</summary>
static class Pixels
{
    private static bool _kept;

    /// <summary>
    /// Starts the engine keeping a copy of each frame presented, which the window's reads need, since
    /// the first read has no frame kept and gives the clear colour. Every frame after pays for the
    /// copy, so it waits for the first zone that reads the window.
    /// </summary>
    public static void Keep()
    {
        if (_kept) return;
        UnloadImage(LoadImageFromScreen());
        _kept = true;
    }

    /// <summary>The average of the nine pixels round a point, within the image.</summary>
    public static Color Average(Image image, Vector2 at)
    {
        int r = 0, g = 0, b = 0;
        for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                var c = GetImageColor(image, Math.Clamp((int)at.X + x, 0, image.Width - 1), Math.Clamp((int)at.Y + y, 0, image.Height - 1));
                (r, g, b) = (r + c.R, g + c.G, b + c.B);
            }
        return new Color((byte)(r / 9), (byte)(g / 9), (byte)(b / 9));
    }

    /// <summary>The mean of several colours.</summary>
    public static Color Mean(Color[] colours) =>
        new((byte)colours.Average(c => c.R), (byte)colours.Average(c => c.G), (byte)colours.Average(c => c.B));

    public static string Text(Color c) => $"{c.R},{c.G},{c.B}";

    public static int Light(Color c) => c.R + c.G + c.B;
}
