using System.Text.Json;

/// <summary>What a tile is, which decides what it costs to cross and what it shelters.</summary>
public enum Terrain { Grass, Forest, Hill, Water }

/// <summary>A kind of unit, with what each can do.</summary>
public enum Kind { Soldier, Archer, Knight }

public enum Side { Blue, Red }

/// <summary>One unit on the board.</summary>
public sealed class Unit
{
    public Kind Kind { get; set; }
    public Side Side { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Health { get; set; }
    public bool Moved { get; set; }
    public bool Acted { get; set; }

    public int MaxHealth => Kind switch { Kind.Soldier => 10, Kind.Archer => 7, _ => 14 };
    public int Attack => Kind switch { Kind.Soldier => 4, Kind.Archer => 3, _ => 5 };
    public int Move => Kind switch { Kind.Soldier => 4, Kind.Archer => 3, _ => 6 };
    public int Range => Kind == Kind.Archer ? 3 : 1;
    public int Sight => Kind switch { Kind.Soldier => 3, Kind.Archer => 5, _ => 4 };
    public bool Alive => Health > 0;
}

/// <summary>
/// The board and its rules: the terrain, the units, whose turn it is, what each side can see, how far
/// a unit can go and by which way, what an attack does, the opponent's moves, and a match written to
/// a file and read back.
/// </summary>
public sealed class Board
{
    public const int Width = 14, Depth = 10;

    public Terrain[,] Tiles { get; private set; } = new Terrain[Width, Depth];
    public List<Unit> Units { get; private set; } = [];
    public Side Turn { get; private set; } = Side.Blue;
    public int Round { get; private set; } = 1;
    public int Seed { get; private set; }

    /// <summary>A new match on a map made from <paramref name="seed"/>, each side's units on its own edge.</summary>
    public static Board New(int seed)
    {
        var board = new Board { Seed = seed };
        board.MakeMap();
        Kind[] line = [Kind.Archer, Kind.Soldier, Kind.Knight, Kind.Soldier, Kind.Archer];
        for (int i = 0; i < line.Length; i++)
        {
            board.Place(line[i], Side.Blue, 0 + (i % 2), 2 + i * 3 / 2);
            board.Place(line[i], Side.Red, Width - 1 - (i % 2), Depth - 3 - i * 3 / 2);
        }
        return board;
    }

    private void Place(Kind kind, Side side, int x, int z)
    {
        Tiles[x, z] = Terrain.Grass;
        var unit = new Unit { Kind = kind, Side = side, X = x, Z = z };
        unit.Health = unit.MaxHealth;
        Units.Add(unit);
    }

    // A lake, woods and hills from the seed, the same map for the same seed, with the edges where
    // the units start kept open.
    private void MakeMap()
    {
        var random = new Random(Seed);
        for (int x = 0; x < Width; x++)
            for (int z = 0; z < Depth; z++)
                Tiles[x, z] = Terrain.Grass;
        void Patch(Terrain terrain, int count, int size)
        {
            for (int i = 0; i < count; i++)
            {
                int cx = random.Next(3, Width - 3), cz = random.Next(1, Depth - 1);
                for (int dx = -size; dx <= size; dx++)
                    for (int dz = -size; dz <= size; dz++)
                        if (Inside(cx + dx, cz + dz) && Math.Abs(dx) + Math.Abs(dz) <= size && random.NextDouble() < 0.8)
                            Tiles[cx + dx, cz + dz] = terrain;
            }
        }
        Patch(Terrain.Forest, 4, 1);
        Patch(Terrain.Hill, 3, 1);
        Patch(Terrain.Water, 1, 1);
    }

    public static bool Inside(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

    public Unit? UnitAt(int x, int z) => Units.FirstOrDefault(u => u.Alive && u.X == x && u.Z == z);

    // What a step onto a tile costs, or none for one that cannot be crossed.
    private static int? Cost(Terrain terrain) => terrain switch { Terrain.Grass => 1, Terrain.Water => null, _ => 2 };

    private static readonly (int X, int Z)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>
    /// Every tile a unit can reach this turn and the cheapest way there, by Dijkstra's search over
    /// the tiles' costs, through its own side's units and never through the other's, ending only
    /// on an empty tile.
    /// </summary>
    public Dictionary<(int X, int Z), (int Cost, (int X, int Z) From)> Reach(Unit unit)
    {
        var best = new Dictionary<(int X, int Z), (int Cost, (int X, int Z) From)> { [(unit.X, unit.Z)] = (0, (unit.X, unit.Z)) };
        var open = new PriorityQueue<(int X, int Z), int>();
        open.Enqueue((unit.X, unit.Z), 0);
        while (open.TryDequeue(out var at, out var spent))
        {
            if (spent > best[at].Cost) continue;
            foreach (var (dx, dz) in Steps)
            {
                var next = (X: at.X + dx, Z: at.Z + dz);
                if (!Inside(next.X, next.Z) || Cost(Tiles[next.X, next.Z]) is not { } cost) continue;
                if (UnitAt(next.X, next.Z) is { } other && other.Side != unit.Side) continue;
                var total = spent + cost;
                if (total > unit.Move || (best.TryGetValue(next, out var known) && known.Cost <= total)) continue;
                best[next] = (total, at);
                open.Enqueue(next, total);
            }
        }
        // A tile another unit stands on can be passed through and not stopped on.
        foreach (var tile in best.Keys.ToList())
            if (tile != (unit.X, unit.Z) && UnitAt(tile.X, tile.Z) is not null) best.Remove(tile);
        return best;
    }

    /// <summary>The tiles from where a unit stands to a tile it can reach, in order, its own excluded.</summary>
    public List<(int X, int Z)> PathTo(Unit unit, (int X, int Z) to)
    {
        var reach = Reach(unit);
        var path = new List<(int X, int Z)>();
        if (!reach.ContainsKey(to)) return path;
        // The search's predecessors are kept for tiles passed through too, so walk them again.
        var all = ReachThrough(unit);
        for (var at = to; at != (unit.X, unit.Z); at = all[at].From) path.Add(at);
        path.Reverse();
        return path;
    }

    // The same search with the tiles friends stand on kept, for walking a path back through them.
    private Dictionary<(int X, int Z), (int Cost, (int X, int Z) From)> ReachThrough(Unit unit)
    {
        var best = new Dictionary<(int X, int Z), (int Cost, (int X, int Z) From)> { [(unit.X, unit.Z)] = (0, (unit.X, unit.Z)) };
        var open = new PriorityQueue<(int X, int Z), int>();
        open.Enqueue((unit.X, unit.Z), 0);
        while (open.TryDequeue(out var at, out var spent))
        {
            if (spent > best[at].Cost) continue;
            foreach (var (dx, dz) in Steps)
            {
                var next = (X: at.X + dx, Z: at.Z + dz);
                if (!Inside(next.X, next.Z) || Cost(Tiles[next.X, next.Z]) is not { } cost) continue;
                if (UnitAt(next.X, next.Z) is { } other && other.Side != unit.Side) continue;
                var total = spent + cost;
                if (total > unit.Move || (best.TryGetValue(next, out var known) && known.Cost <= total)) continue;
                best[next] = (total, at);
                open.Enqueue(next, total);
            }
        }
        return best;
    }

    /// <summary>
    /// Every tile a side's units can see: those within each unit's sight, one further from a hill,
    /// that no woods stand between. A unit sees into woods and not past them.
    /// </summary>
    public HashSet<(int X, int Z)> Seen(Side side)
    {
        var seen = new HashSet<(int X, int Z)>();
        foreach (var unit in Units.Where(u => u.Alive && u.Side == side))
        {
            var sight = unit.Sight + (Tiles[unit.X, unit.Z] == Terrain.Hill ? 1 : 0);
            for (int dx = -sight; dx <= sight; dx++)
                for (int dz = -sight; dz <= sight; dz++)
                {
                    var tile = (X: unit.X + dx, Z: unit.Z + dz);
                    if (Math.Abs(dx) + Math.Abs(dz) <= sight && Inside(tile.X, tile.Z) && Clear((unit.X, unit.Z), tile))
                        seen.Add(tile);
                }
        }
        return seen;
    }

    // Whether no woods stand on the tiles a line between two tiles crosses, the two left out.
    private bool Clear((int X, int Z) from, (int X, int Z) to)
    {
        var steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Z - from.Z));
        for (int i = 1; i < steps; i++)
        {
            var x = (int)MathF.Round(from.X + (to.X - from.X) * i / (float)steps);
            var z = (int)MathF.Round(from.Z + (to.Z - from.Z) * i / (float)steps);
            if (Tiles[x, z] == Terrain.Forest && (x, z) != from && (x, z) != to) return false;
        }
        return true;
    }

    /// <summary>
    /// The way to the tile a unit can reach this turn that comes nearest a goal, near enough when
    /// within <paramref name="range"/> of it, the cheaper of two as near, leaving out the tiles
    /// <paramref name="claimed"/> by others ordered with it. None when it is as near where it stands.
    /// </summary>
    public List<(int X, int Z)> Approach(Unit unit, (int X, int Z) goal, int range, ISet<(int X, int Z)> claimed)
    {
        int Short((int X, int Z) t) => Math.Max(0, Guess(t, goal) - range);
        var stop = Reach(unit).Where(r => r.Key == (unit.X, unit.Z) || !claimed.Contains(r.Key))
            .OrderBy(r => Short(r.Key)).ThenBy(r => r.Value.Cost).ThenBy(r => r.Key.X).ThenBy(r => r.Key.Z).First().Key;
        claimed.Add(stop);
        return stop == (unit.X, unit.Z) || Short(stop) >= Short((unit.X, unit.Z)) ? [] : PathTo(unit, stop);
    }

    /// <summary>
    /// The cheapest way from a unit to any tile beside a target, by A* with the distance across the
    /// grid as its guess, however many turns it takes, or none where the target cannot be reached.
    /// </summary>
    public List<(int X, int Z)> Route(Unit unit, (int X, int Z) target)
    {
        var cost = new Dictionary<(int X, int Z), int> { [(unit.X, unit.Z)] = 0 };
        var from = new Dictionary<(int X, int Z), (int X, int Z)>();
        var open = new PriorityQueue<(int X, int Z), int>();
        open.Enqueue((unit.X, unit.Z), Guess((unit.X, unit.Z), target));
        while (open.TryDequeue(out var at, out _))
        {
            if (Math.Abs(at.X - target.X) + Math.Abs(at.Z - target.Z) <= 1 && at != target)
            {
                var path = new List<(int X, int Z)>();
                for (var step = at; step != (unit.X, unit.Z); step = from[step]) path.Add(step);
                path.Reverse();
                return path;
            }
            foreach (var (dx, dz) in Steps)
            {
                var next = (X: at.X + dx, Z: at.Z + dz);
                if (!Inside(next.X, next.Z) || Cost(Tiles[next.X, next.Z]) is not { } step) continue;
                if (next != target && UnitAt(next.X, next.Z) is { } other && other.Side != unit.Side) continue;
                var total = cost[at] + step;
                if (cost.TryGetValue(next, out var known) && known <= total) continue;
                cost[next] = total;
                from[next] = at;
                open.Enqueue(next, total + Guess(next, target));
            }
        }
        return [];
    }

    private static int Guess((int X, int Z) a, (int X, int Z) b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

    public static int Distance(Unit a, Unit b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

    public bool CanAttack(Unit attacker, Unit target) =>
        attacker.Alive && target.Alive && attacker.Side != target.Side && !attacker.Acted && Distance(attacker, target) <= attacker.Range;

    /// <summary>What an attack takes, less for a target sheltered by woods or high on a hill, never under one.</summary>
    public int Damage(Unit attacker, Unit target) =>
        Math.Max(1, attacker.Attack - (Tiles[target.X, target.Z] is Terrain.Forest or Terrain.Hill ? 1 : 0));

    public void Strike(Unit attacker, Unit target)
    {
        target.Health = Math.Max(0, target.Health - Damage(attacker, target));
        attacker.Acted = attacker.Moved = true;
    }

    public void MoveTo(Unit unit, (int X, int Z) to)
    {
        (unit.X, unit.Z) = to;
        unit.Moved = true;
    }

    public Side? Winner =>
        Units.Any(u => u.Alive && u.Side == Side.Blue) ? Units.Any(u => u.Alive && u.Side == Side.Red) ? null : Side.Blue : Side.Red;

    public void EndTurn()
    {
        Turn = Turn == Side.Blue ? Side.Red : Side.Blue;
        if (Turn == Side.Blue) Round++;
        foreach (var unit in Units) unit.Moved = unit.Acted = false;
    }

    /// <summary>
    /// What the opponent does with one unit, knowing only what its side sees: strike the weakest
    /// enemy in sight and in range, or else walk as far along the cheapest way toward the nearest
    /// enemy in sight as it can this turn and strike from there. With none in sight it heads for
    /// the edge the other side started from, to find them.
    /// </summary>
    public (List<(int X, int Z)> Path, Unit? Target) Plan(Unit unit)
    {
        var seen = Seen(unit.Side);
        var enemies = Units.Where(e => e.Alive && e.Side != unit.Side && seen.Contains((e.X, e.Z))).ToList();
        if (enemies.Where(e => CanAttack(unit, e)).OrderBy(e => e.Health).ThenBy(e => e.X).ThenBy(e => e.Z).FirstOrDefault() is { } now)
            return ([], now);

        foreach (var enemy in enemies.OrderBy(e => Distance(unit, e)).ThenBy(e => e.X).ThenBy(e => e.Z))
        {
            var path = Toward(unit, (enemy.X, enemy.Z), unit.Range);
            if (path is null) continue;
            var (x, z) = (unit.X, unit.Z);
            if (path.Count > 0) (unit.X, unit.Z) = path[^1];
            var target = enemies.Where(e => Distance(unit, e) <= unit.Range).OrderBy(e => e.Health).ThenBy(e => e.X).FirstOrDefault();
            (unit.X, unit.Z) = (x, z);
            return (path, target);
        }
        var edge = (X: unit.Side == Side.Red ? 1 : Width - 2, unit.Z);
        return (Toward(unit, edge, 0) ?? [], null);
    }

    // The way along the cheapest route toward a tile as far as a unit can stop this turn, stopping
    // once within range of it, or none where no route reaches it.
    private List<(int X, int Z)>? Toward(Unit unit, (int X, int Z) goal, int range)
    {
        if (Guess((unit.X, unit.Z), goal) <= range) return [];
        var route = Route(unit, goal);
        if (route.Count == 0) return null;
        var reach = Reach(unit);
        var stop = (X: unit.X, Z: unit.Z);
        foreach (var tile in route)
        {
            if (!reach.ContainsKey(tile)) continue;
            stop = tile;
            if (Guess(tile, goal) <= range) break;
        }
        return stop == (unit.X, unit.Z) ? [] : PathTo(unit, stop);
    }

    // -- A match written to a file and read back

    private sealed record Saved(int Seed, Side Turn, int Round, List<Unit> Units);

    public string Save() => JsonSerializer.Serialize(new Saved(Seed, Turn, Round, Units), Json);

    /// <summary>A match read back from what <see cref="Save"/> wrote, or none for text that is not one.</summary>
    public static Board? Load(string text)
    {
        try
        {
            if (JsonSerializer.Deserialize<Saved>(text, Json) is not { } saved) return null;
            var board = new Board { Seed = saved.Seed, Turn = saved.Turn, Round = saved.Round, Units = saved.Units };
            board.MakeMap();
            return board;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}
