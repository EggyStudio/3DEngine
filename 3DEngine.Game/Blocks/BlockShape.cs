using System.Numerics;

namespace Engine.Game;

/// <summary>
/// Which way a block smaller than its cell faces: up where it stands on the block below, down where
/// it hangs from the block above, and toward a side where it is fixed to the block behind it, as
/// Minecraft's torch on a wall faces away from the wall. North is toward -z and east toward +x, as
/// Minecraft has them.
/// </summary>
public enum Facing
{
    Up,
    Down,
    North,
    South,
    West,
    East,
}

/// <summary>
/// A box of a block smaller than its cell, between two corners in the cell's own space from 0 to 1,
/// of one surface, its top face moved by <see cref="Lean"/>, as a torch on a wall leans out from it.
/// </summary>
public readonly record struct Piece(Vector3 From, Vector3 To, int Surface, Vector3 Lean = default)
{
    /// <summary>Whether its surface gives off light, so it is drawn as a lamp's cube rather than as faces of its section.</summary>
    public bool Glows => Surfaces.All[Surface].Emits;
}

/// <summary>
/// The boxes a block smaller than its cell is made of, as a torch, a lantern or an end rod is, the
/// way it faces and the box around them all, which the crosshair meets and the outline is drawn on.
/// </summary>
/// <remarks>
/// A box that gives off light is drawn as a lamp's cube of its own scaled to it, square to the axes,
/// so the light that bounces carries it as a small light by its box, and the other boxes are faces
/// of its section's mesh. Light and the player pass through such a block, it hides none of the faces
/// beside it, and it falls when the block it holds to is broken.
/// </remarks>
public sealed class BlockShape
{
    public BlockShape(Facing facing, params Piece[] pieces)
    {
        Facing = facing;
        Pieces = pieces;
        (Min, Max) = (Vector3.One, Vector3.Zero);
        foreach (var piece in pieces)
        {
            if (piece.Glows && piece.Lean != Vector3.Zero) throw new ArgumentException("A box that gives off light is drawn as a cube square to the axes, so it cannot lean.");
            foreach (var corner in Corners(piece))
            {
                Min = Vector3.Min(Min, corner);
                Max = Vector3.Max(Max, corner);
            }
        }
    }

    public Facing Facing { get; }

    public IReadOnlyList<Piece> Pieces { get; }

    /// <summary>The corner of the box around every piece nearest the cell's origin.</summary>
    public Vector3 Min { get; }

    /// <summary>The corner of the box around every piece farthest from the cell's origin.</summary>
    public Vector3 Max { get; }

    /// <summary>Whether any of its pieces gives off light.</summary>
    public bool Glows => Pieces.Any(p => p.Glows);

    /// <summary>The step from its cell to the block it holds to, the one behind the way it faces.</summary>
    public (int X, int Y, int Z) Support => Facing switch
    {
        Facing.Up => (0, -1, 0),
        Facing.Down => (0, 1, 0),
        Facing.North => (0, 0, 1),
        Facing.South => (0, 0, -1),
        Facing.West => (1, 0, 0),
        _ => (-1, 0, 0),
    };

    /// <summary>The way a block placed against a face faces, away from the block whose face it is, from that face's outward normal.</summary>
    public static Facing Away(int x, int y, int z) => (x, y, z) switch
    {
        (0, 1, 0) => Facing.Up,
        (0, -1, 0) => Facing.Down,
        (0, 0, -1) => Facing.North,
        (0, 0, 1) => Facing.South,
        (-1, 0, 0) => Facing.West,
        _ => Facing.East,
    };

    /// <summary>A piece's eight corners, its top four moved by its lean.</summary>
    public static IEnumerable<Vector3> Corners(Piece piece)
    {
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
            yield return piece.From + corner * (piece.To - piece.From) + corner.Y * piece.Lean;
        }
    }

    // A piece given in sixteenths of a block, as Minecraft's models give theirs.
    private static Piece Px(float x0, float y0, float z0, float x1, float y1, float z1, int surface, Vector3 lean = default) =>
        new(new Vector3(x0, y0, z0) / 16, new Vector3(x1, y1, z1) / 16, surface, lean / 16);

    /// <summary>A torch standing on the block below, a stick with a flame at its top.</summary>
    public static BlockShape Torch(int flame) => new(Facing.Up,
        Px(7, 0, 7, 9, 7, 9, Surfaces.TorchStick),
        Px(7, 7, 7, 9, 10, 9, flame));

    /// <summary>
    /// A torch fixed to the wall behind it, its stick leaning out from the wall's face by about 23
    /// degrees as Minecraft's does, and its flame upright at the stick's top.
    /// </summary>
    public static BlockShape WallTorch(Facing facing, int flame) => new(facing, Yawed(facing,
        Px(7, 3, 14, 9, 10, 16, Surfaces.TorchStick, new Vector3(0, 0, -3)),
        Px(7, 10, 11, 9, 13, 13, flame)));

    /// <summary>A lantern standing on the block below, its glass lit and its cap and handle of dark iron.</summary>
    public static BlockShape Lantern(int glass) => new(Facing.Up,
        Px(5, 0, 5, 11, 7, 11, glass),
        Px(6, 7, 6, 10, 9, 10, Surfaces.Iron),
        Px(7, 9, 7.5f, 9, 11, 8.5f, Surfaces.Iron));

    /// <summary>A lantern hanging from the block above by a short chain.</summary>
    public static BlockShape HangingLantern(int glass) => new(Facing.Down,
        Px(5, 1, 5, 11, 8, 11, glass),
        Px(6, 8, 6, 10, 10, 10, Surfaces.Iron),
        Px(7.5f, 10, 7.5f, 8.5f, 16, 8.5f, Surfaces.Iron));

    /// <summary>An end rod, a white rod that gives off light on a small base, its base on the block it is fixed to.</summary>
    public static BlockShape EndRod(Facing facing) => new(facing, Turned(facing,
        Px(6, 0, 6, 10, 1, 10, Surfaces.EndRodBase),
        Px(7, 1, 7, 9, 16, 9, Surfaces.EndRod)));

    // Pieces given facing north, a wall behind them at +z, turned about the cell's upright middle line
    // to face another side.
    private static Piece[] Yawed(Facing facing, params Piece[] pieces) => [.. pieces.Select(p => Moved(p, c => facing switch
    {
        Facing.South => new Vector3(-c.X, c.Y, -c.Z),
        Facing.East => new Vector3(-c.Z, c.Y, c.X),
        Facing.West => new Vector3(c.Z, c.Y, -c.X),
        _ => c,
    }))];

    // Pieces given facing up, standing on the block below, turned about the cell's middle to face
    // another way. A piece that leans cannot be turned off its upright, so none of these leans.
    private static Piece[] Turned(Facing facing, params Piece[] pieces) => [.. pieces.Select(p => Moved(p, c => facing switch
    {
        Facing.Down => new Vector3(c.X, -c.Y, -c.Z),
        Facing.North => new Vector3(c.X, c.Z, -c.Y),
        Facing.South => new Vector3(c.X, -c.Z, c.Y),
        Facing.East => new Vector3(c.Y, -c.X, c.Z),
        Facing.West => new Vector3(-c.Y, c.X, c.Z),
        _ => c,
    }))];

    // A piece's corners and lean taken through a turn about the cell's middle, its corners sorted
    // again so its From is the lower.
    private static Piece Moved(Piece piece, Func<Vector3, Vector3> turn)
    {
        var half = new Vector3(0.5f);
        Vector3 a = turn(piece.From - half) + half, b = turn(piece.To - half) + half;
        return piece with { From = Vector3.Min(a, b), To = Vector3.Max(a, b), Lean = turn(piece.Lean) };
    }
}
