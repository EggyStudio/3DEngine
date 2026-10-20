using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The drawing calls: every 3D shape on a plinth of its own along the road, a grid laid through
/// rlgl's matrix, and an easel facing the player that shows a texture 2D shapes and text are drawn
/// into each frame.
/// </summary>
/// <remarks>
/// Every two seconds the easel's texture is read back from the GPU and each shape's colour looked
/// for at a pixel inside it, the text by a dark pixel within the box it measures, so the status says
/// what reached the texture, and the easel is ok only when every shape did.
/// </remarks>
sealed class Drawing : Zone
{
    private const int Side = 256;
    private static readonly Color Paper = new(245, 240, 228);
    private readonly RenderTexture2D _easel = LoadRenderTexture(Side, Side);
    private readonly (string Name, Action<Vector3> Draw)[] _shapes;
    private float _time;
    private int _frames, _reads, _found = -1;
    private string _missing = "";

    // Each shape drawn into the easel, its colour, and a pixel inside it where that colour is read.
    private static readonly (string Name, Color Color, int X, int Y)[] Marks =
    [
        ("rectangle", new Color(220, 50, 50), 52, 40),
        ("circle", new Color(40, 90, 210), 160, 44),
        ("polygon", new Color(240, 200, 40), 222, 44),
        ("triangle", new Color(40, 160, 70), 52, 121),
        ("ring", new Color(240, 130, 30), 181, 112),
        ("rounded rectangle", new Color(150, 70, 190), 221, 112),
        ("thick line", new Color(30, 160, 160), 68, 168),
        ("ellipse", new Color(200, 80, 140), 52, 214),
    ];

    public Drawing()
    {
        var red = new Color(210, 70, 60);
        var blue = new Color(60, 110, 210);
        var green = new Color(60, 170, 90);
        _shapes =
        [
            ("cube", p => DrawCube(p, 0.9f, 0.9f, 0.9f, red)),
            ("cube's edges", p => DrawCubeWires(p, 0.9f, 0.9f, 0.9f, blue)),
            ("cube by a vector", p => DrawCubeV(p, new Vector3(1.1f, 0.5f, 0.7f), green)),
            ("cube's edges by a vector", p => DrawCubeWiresV(p, new Vector3(1.1f, 0.5f, 0.7f), red)),
            ("sphere", p => DrawSphere(p, 0.5f, blue)),
            ("sphere of few rings", p => DrawSphereEx(p, 0.5f, 6, 8, green)),
            ("sphere's rings", p => DrawSphereWires(p, 0.5f, 8, 12, red)),
            ("cone", p => DrawCylinder(p - new Vector3(0, 0.5f, 0), 0, 0.5f, 1, 16, blue)),
            ("cylinder's edges", p => DrawCylinderWires(p - new Vector3(0, 0.5f, 0), 0.4f, 0.4f, 1, 12, green)),
            ("cylinder between points", p => DrawCylinderEx(p - new Vector3(0.4f, 0.3f, 0), p + new Vector3(0.4f, 0.3f, 0), 0.2f, 0.35f, 12, red)),
            ("cylinder's edges between points", p => DrawCylinderWiresEx(p - new Vector3(0.4f, 0.3f, 0), p + new Vector3(0.4f, 0.3f, 0), 0.2f, 0.35f, 12, blue)),
            ("capsule", p => DrawCapsule(p - new Vector3(0, 0.3f, 0), p + new Vector3(0, 0.3f, 0), 0.3f, 8, 12, green)),
            ("capsule's edges", p => DrawCapsuleWires(p - new Vector3(0, 0.3f, 0), p + new Vector3(0, 0.3f, 0), 0.3f, 8, 12, red)),
            ("plane", p => DrawPlane(p - new Vector3(0, 0.4f, 0), new Vector2(1, 1), blue)),
            ("line", p => DrawLine3D(p - new Vector3(0.5f, 0.4f, 0), p + new Vector3(0.5f, 0.4f, 0), green)),
            ("point", p => DrawPoint3D(p, red)),
            ("circle", p => DrawCircle3D(p, 0.5f, Vector3.UnitY, _time * 40, blue)),
            ("triangle", p => DrawTriangle3D(p + new Vector3(-0.5f, -0.4f, 0), p + new Vector3(0.5f, -0.4f, 0), p + new Vector3(0, 0.5f, 0), green)),
            ("triangle strip", p => DrawTriangleStrip3D(
                [p + new Vector3(-0.5f, -0.4f, 0), p + new Vector3(-0.5f, 0.4f, 0), p + new Vector3(0, -0.4f, 0), p + new Vector3(0, 0.4f, 0), p + new Vector3(0.5f, -0.4f, 0)], red)),
            ("ray", p => DrawRay(new Ray(p - new Vector3(0, 0.4f, 0), Vector3.Normalize(new Vector3(0.2f, 1, 0.1f))), blue)),
            ("bounding box", p => DrawBoundingBox(new BoundingBox(p - new Vector3(0.4f), p + new Vector3(0.4f)), green)),
            ("grid through rlgl", p =>
            {
                rlPushMatrix();
                rlTranslatef(p.X, p.Y - 0.45f, p.Z);
                DrawGrid(4, 0.25f);
                rlPopMatrix();
            }),
        ];
    }

    public override string Name => "Drawing";
    public override Vector3 Center => Zones.At(270);
    public override Vector3 Start => Center + new Vector3(14, 0, 0);

    // The plinth of each shape, in two rows either side of the road.
    private Vector3 Plinth(int i) => Center + new Vector3(12 - (i / 2) * 2.4f, 0, i % 2 == 0 ? -4 : 4);

    public override void Update(float dt, Player player)
    {
        _time += dt;
        // The texture is read once it has been drawn a few frames, and every two seconds after.
        if (++_frames % 120 != 10) return;
        var image = LoadImageFromTexture(_easel.Texture);
        if (!IsImageValid(image)) return;
        var missing = new List<string>();
        foreach (var (name, color, x, y) in Marks)
            if (!Near(GetImageColor(image, x, y), color)) missing.Add(name);
        // The gradient runs from black at its left to white at its right.
        if (!(GetImageColor(image, 140, 168).R < 60 && GetImageColor(image, 242, 168).R > 200)) missing.Add("gradient");
        // The text, by any dark pixel in the box it measures.
        var width = MeasureText("Easel", 20);
        var dark = false;
        for (var y = 200; y < 220 && !dark; y++)
            for (var x = 110; x < 110 + width && !dark; x++)
                dark = GetImageColor(image, x, y).R < 100;
        if (!dark) missing.Add("text");
        UnloadImage(image);
        _reads++;
        _found = Marks.Length + 2 - missing.Count;
        _missing = string.Join(", ", missing);
    }

    private static bool Near(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 12 && Math.Abs(a.G - b.G) <= 12 && Math.Abs(a.B - b.B) <= 12;

    public override void DrawOffscreen()
    {
        BeginTextureMode(_easel);
        ClearBackground(Paper);
        DrawRectangle(16, 16, 72, 48, Marks[0].Color);
        DrawCircle(160, 44, 28, Marks[1].Color);
        DrawPoly(new Vector2(222, 44), 6, 24, 0, Marks[2].Color);
        DrawTriangle(new Vector2(52, 84), new Vector2(16, 140), new Vector2(88, 140), Marks[3].Color);
        DrawRing(new Vector2(160, 112), 14, 28, 0, 360, 36, Marks[4].Color);
        DrawRectangleRounded(new Rectangle(196, 88, 50, 48), 0.4f, 8, Marks[5].Color);
        DrawLineEx(new Vector2(16, 168), new Vector2(120, 168), 10, Marks[6].Color);
        DrawRectangleGradientH(136, 154, 110, 28, Color.Black, Color.White);
        DrawEllipse(52, 214, 36, 18, Marks[7].Color);
        DrawText("Easel", 110, 200, 20, new Color(20, 20, 20));
        // A sector turning, which the read passes over.
        DrawCircleSector(new Vector2(222, 222), 22, _time * 90, _time * 90 + 120, 16, new Color(90, 90, 90));
        DrawRectangleLines(0, 0, Side, Side, new Color(120, 100, 80));
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        for (var i = 0; i < _shapes.Length; i++)
        {
            var plinth = Plinth(i);
            DrawCube(plinth + new Vector3(0, 0.5f, 0), 1.3f, 1, 1.3f, new Color(170, 165, 155));
            _shapes[i].Draw(plinth + new Vector3(0, 1.75f, 0));
        }
        DrawCylinder(Center + new Vector3(-1, 0, 0), 0.06f, 0.06f, 1.2f, 6, new Color(110, 80, 50));
        DrawBillboard(camera, _easel.Texture, Center + new Vector3(-1, 2.6f, 0), 3, Color.White);
    }

    public override string Status()
    {
        var easel = _found < 0
            ? "the easel not read yet"
            : $"the easel's {_found} of {Marks.Length + 2} shapes found at their pixels{(_missing.Length > 0 ? $", missing {_missing}" : "")} in {_reads} reads";
        return $"{_shapes.Length + 1} 3D calls a frame, {easel}{(_found == Marks.Length + 2 ? ", easel ok" : "")}";
    }
}
