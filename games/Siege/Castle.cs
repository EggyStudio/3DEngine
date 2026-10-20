using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>
/// The castle the catapult throws at, a keep of blocks round a hollow middle under a slab, a roof
/// and a banner, a wall either side of it and a low wall before it, every block welded to the
/// blocks it was laid against and the lowest to a foundation, a weld breaking where it holds
/// harder than its strength and a block going where what strikes it has pressed it too hard.
/// </summary>
public sealed class Castle
{
    // The force a weld breaks past, in newtons, and the push a block takes before it goes, in
    // kilogram meters a second over the steps something thrown presses it. The keep's welds hold
    // 5,064 newtons at most standing, a stone striking it puts 9,000 to 14,000 on the welds near
    // it, and a stone thrown hard presses the block it meets 430 to 480 over a step.
    public const float Strength = 8000;
    public const float Toughness = 450;
    public const float BlockMass = 40;

    // The keep's middle across the field, and its height in blocks.
    public static readonly Vector3 Keep = new(30, 0, 0);
    public const int KeepLayers = 12;

    public enum Shape { Box, Column, Slab, Roof, Banner }

    public sealed class Block(PhysicsBody body, Shape shape, Color color)
    {
        public readonly PhysicsBody Body = body;
        public readonly Shape Shape = shape;
        public readonly Color Color = color;
        public float Damage;
        public bool Gone;
    }

    public sealed class Weld(PhysicsJoint joint, Block a, Block b)
    {
        public readonly PhysicsJoint Joint = joint;
        public readonly Block A = a, B = b;
        public float Force;
        public bool Broken;
    }

    public readonly List<Block> Blocks = [];
    public readonly List<Weld> Welds = [];
    public Block Banner { get; private set; } = null!;
    public int Broken => Welds.Count(w => w.Broken);
    public int Standing => Blocks.Count(b => !b.Gone);

    private readonly Models _models;
    private readonly PhysicsBody _foundation;
    private readonly Block _ground;
    private readonly Dictionary<(int X, int Y, int Z), Block> _cells = [];

    /// <summary>
    /// The models the blocks are drawn with, from which the hulls of the shapes that are not boxes
    /// are made.
    /// </summary>
    public sealed record Models(Model Cube, Model Column, Model Roof);

    public Castle(Models models)
    {
        _models = models;
        // Kinematic, since a weld holds bodies that move, and level with the field.
        _foundation = CreatePhysicsKinematicBox(Keep with { Y = -0.25f }, new Vector3(30, 0.5f, 30));
        _ground = new Block(_foundation, Shape.Box, default);
        Build();
    }

    /// <summary>Takes every block down and lays the castle again.</summary>
    public void Rebuild()
    {
        foreach (var weld in Welds.Where(w => !w.Broken && IsPhysicsJointValid(w.Joint))) DestroyPhysicsJoint(weld.Joint);
        foreach (var block in Blocks.Where(b => !b.Gone)) DestroyPhysicsBody(block.Body);
        Welds.Clear();
        Blocks.Clear();
        _cells.Clear();
        Build();
    }

    private void Build()
    {
        var x0 = (int)Keep.X;
        // The keep, a ring of eight a layer round a hollow middle, its corners columns.
        for (int y = 0; y < KeepLayers; y++)
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var corner = dx != 0 && dz != 0;
                    Lay(x0 + dx, y, dz, corner ? Shape.Column : Shape.Box, Stone(x0 + dx, y, dz));
                }
        // A wall either side, five high with a battlement on every other block.
        foreach (var side in new[] { -1, 1 })
        {
            for (int along = 2; along <= 7; along++)
                for (int y = 0; y < 5; y++) Lay(x0, y, side * along, Shape.Box, Stone(x0, y, side * along));
            for (int along = 2; along <= 7; along += 2) Lay(x0, 5, side * along, Shape.Box, Stone(x0, 5, side * along));
        }
        // A low wall before the keep, which a shot thrown too low meets first.
        for (int z = -3; z <= 3; z++)
            for (int y = 0; y < 3; y++) Lay(x0 - 6, y, z, Shape.Box, Stone(x0 - 6, y, z));
        for (int z = -3; z <= 3; z += 2) Lay(x0 - 6, 3, z, Shape.Box, Stone(x0 - 6, 3, z));

        // A slab over the keep's ring, a roof on it and a banner on the roof, each welded to what
        // it stands on.
        var slabAt = new Vector3(x0, KeepLayers + 0.25f, 0);
        var slab = Add(CreatePhysicsBox(slabAt, new Vector3(3, 0.5f, 3), BlockMass * 2), Shape.Slab, new Color(110, 100, 95));
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (_cells.TryGetValue((x0 + dx, KeepLayers - 1, dz), out var below)) Join(below, slab);
        var roof = Add(CreatePhysicsConvexHull(_models.Roof, slabAt with { Y = KeepLayers + 0.5f }, BlockMass * 2), Shape.Roof, new Color(150, 60, 50));
        Join(slab, roof);
        var bannerAt = new Vector3(x0, KeepLayers + 0.5f + RoofHeight + 0.8f, 0);
        Banner = Add(CreatePhysicsBox(bannerAt, new Vector3(0.15f, 1.6f, 0.15f), 5), Shape.Banner, new Color(240, 210, 80));
        Join(roof, Banner);
    }

    public const float RoofHeight = 2.5f;

    // The top of the walls either side, five blocks and a battlement high.
    public const float WallTop = 6;

    // A gray a little different for each block, so the courses read.
    private static Color Stone(int x, int y, int z)
    {
        var shade = 150 + (int)((uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791) % 40);
        return new Color((byte)shade, (byte)(shade - 8), (byte)(shade - 18));
    }

    // A block in a cell of the grid a meter on a side, welded to the block below it, or to the
    // foundation, and to those laid before it beside it.
    private void Lay(int x, int y, int z, Shape shape, Color color)
    {
        var center = new Vector3(x, y + 0.5f, z);
        var body = shape == Shape.Column
            ? CreatePhysicsConvexHull(_models.Column, center with { Y = y }, BlockMass)
            : CreatePhysicsBox(center, Vector3.One, BlockMass);
        var block = Add(body, shape, color);
        _cells[(x, y, z)] = block;
        Join(y == 0 ? _ground : _cells.GetValueOrDefault((x, y - 1, z)), block);
        Join(_cells.GetValueOrDefault((x - 1, y, z)), block);
        Join(_cells.GetValueOrDefault((x, y, z - 1)), block);
    }

    private Block Add(PhysicsBody body, Shape shape, Color color)
    {
        var block = new Block(body, shape, color);
        Blocks.Add(block);
        return block;
    }

    private void Join(Block? a, Block b)
    {
        if (a is null) return;
        Welds.Add(new Weld(CreatePhysicsWeldJoint(a.Body, b.Body), a, b));
    }

    /// <summary>
    /// Reads every weld's force over the last step, breaking each past the strength, and marks
    /// the welds a block took with it when it went. Says how many broke.
    /// </summary>
    public int Strain()
    {
        var broke = 0;
        foreach (var weld in Welds)
        {
            if (weld.Broken) continue;
            if (!IsPhysicsJointValid(weld.Joint))
            {
                weld.Broken = true;
                continue;
            }
            weld.Force = GetPhysicsJointForce(weld.Joint);
            if (weld.Force <= Strength) continue;
            DestroyPhysicsJoint(weld.Joint);
            weld.Broken = true;
            broke++;
        }
        return broke;
    }

    /// <summary>Takes a block away, its welds with it, as one struck too hard crumbles.</summary>
    public void Crumble(Block block)
    {
        if (block.Gone || block.Shape == Shape.Banner) return;
        DestroyPhysicsBody(block.Body);
        block.Gone = true;
    }

    /// <summary>The block a body is, if it is one standing.</summary>
    public Block? Of(PhysicsBody body) => Blocks.FirstOrDefault(b => !b.Gone && b.Body == body);

    /// <summary>Draws every block standing.</summary>
    public void Draw()
    {
        foreach (var block in Blocks)
        {
            if (block.Gone) continue;
            var at = GetPhysicsBodyTransform(block.Body);
            switch (block.Shape)
            {
                case Shape.Column:
                    _models.Column.Transform = at;
                    DrawModel(_models.Column, Vector3.Zero, 1, block.Color);
                    break;
                case Shape.Roof:
                    _models.Roof.Transform = at;
                    DrawModel(_models.Roof, Vector3.Zero, 1, block.Color);
                    break;
                case Shape.Slab:
                    _models.Cube.Transform = Matrix4x4.CreateScale(3, 0.5f, 3) * at;
                    DrawModel(_models.Cube, Vector3.Zero, 1, block.Color);
                    break;
                case Shape.Banner:
                    _models.Cube.Transform = Matrix4x4.CreateScale(0.15f, 1.6f, 0.15f) * at;
                    DrawModel(_models.Cube, Vector3.Zero, 1, new Color(90, 70, 50));
                    // The flag off the pole's top, turning with it.
                    _models.Cube.Transform = Matrix4x4.CreateScale(0.9f, 0.55f, 0.04f) * Matrix4x4.CreateTranslation(0.5f, 0.45f, 0) * at;
                    DrawModel(_models.Cube, Vector3.Zero, 1, block.Color);
                    break;
                default:
                    _models.Cube.Transform = at;
                    DrawModel(_models.Cube, Vector3.Zero, 1, block.Color);
                    break;
            }
        }
    }
}
