using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The mobs of the loaded columns, those of the columns unloaded kept by their column, and which
/// columns have had their mobs placed, so a column's animals and villagers are placed once, when it
/// is first generated, and come back where they were left each time it loads after.
/// </summary>
/// <remarks>
/// <para>
/// A mob thinks and moves only while the columns around its own are loaded, so it never walks into
/// one that is not and never stands on the air an unloaded column reads as. When a column unloads,
/// the mobs standing in it go with it, and come back with it.
/// </para>
/// <para>
/// Mobs push each other apart and are pushed away by the player, as Minecraft's are, a little each
/// frame by how far their boxes overlap.
/// </para>
/// </remarks>
public sealed class Mobs
{
    // How far the crosshair reaches a mob, as Minecraft's does.
    public const float Reach = 3.5f;

    private readonly List<Mob> _mobs = [];
    private readonly Dictionary<(int X, int Z), List<MobRecord>> _kept;
    private readonly HashSet<(int X, int Z)> _placed;
    private readonly Random _random = new();
    private int _nextId = 1;

    /// <summary>Begins with the mobs a save kept, or none.</summary>
    public Mobs(Dictionary<(int X, int Z), List<MobRecord>>? kept = null, HashSet<(int X, int Z)>? placed = null)
    {
        _kept = kept ?? [];
        _placed = placed ?? [];
    }

    public IReadOnlyList<Mob> All => _mobs;

    /// <summary>Whether the mobs stand still and think nothing, as for a capture.</summary>
    public bool Still { get; set; }

    /// <summary>Mobs that died and were taken away, for the game to drop what they leave and clear.</summary>
    public List<Mob> Died { get; } = [];

    /// <summary>Mobs kept by columns not loaded.</summary>
    public int Kept => _kept.Values.Sum(l => l.Count);

    /// <summary>Puts a mob into the world, a number of its own given.</summary>
    public Mob Add(MobRecord record)
    {
        var mob = new Mob(_nextId++, record, _random.Next());
        _mobs.Add(mob);
        return mob;
    }

    /// <summary>Brings back a column's kept mobs as it loads, or places those its generator gave it the first time.</summary>
    public void Joined(ChunkColumn column)
    {
        var key = (column.X, column.Z);
        if (_placed.Add(key))
        {
            foreach (var spawn in column.Spawns) Add(spawn);
        }
        else if (_kept.Remove(key, out var kept))
            foreach (var record in kept) Add(record);
        column.Spawns.Clear();
    }

    /// <summary>Keeps the mobs standing in a column as it unloads.</summary>
    public void Leaving(int x, int z)
    {
        List<MobRecord>? kept = null;
        _mobs.RemoveAll(mob =>
        {
            if (mob.Dying >= 0 || ColumnOf(mob) != (x, z)) return false;
            (kept ??= []).Add(mob.Record);
            return true;
        });
        if (kept is not null) _kept[(x, z)] = kept;
    }

    private static (int X, int Z) ColumnOf(Mob mob) =>
        (VoxelWorld.ColumnOf((int)MathF.Floor(mob.Body.Position.X)), VoxelWorld.ColumnOf((int)MathF.Floor(mob.Body.Position.Z)));

    /// <summary>Every column that has had its mobs placed, with the mobs it holds, the loaded ones' by where they stand.</summary>
    public IEnumerable<((int X, int Z) Column, IReadOnlyList<MobRecord> Mobs)> Columns()
    {
        var loaded = new Dictionary<(int, int), List<MobRecord>>();
        foreach (var mob in _mobs)
            if (mob.Dying < 0)
            {
                var key = ColumnOf(mob);
                if (!loaded.TryGetValue(key, out var list)) loaded[key] = list = [];
                list.Add(mob.Record);
            }
        foreach (var key in _placed)
        {
            if (loaded.TryGetValue(key, out var here)) yield return (key, here);
            else if (_kept.TryGetValue(key, out var kept)) yield return (key, kept);
            else yield return (key, []);
        }
    }

    /// <summary>Thinks and moves every mob whose columns are loaded around it, pushes them apart, and takes away the dead.</summary>
    public void Update(VoxelWorld world, Body player, Vector3 playerEye, float hour, float seconds)
    {
        if (Still) return;
        foreach (var mob in _mobs)
        {
            var (cx, cz) = ColumnOf(mob);
            if (!Around(world, cx, cz)) continue;
            mob.Update(world, playerEye, hour, seconds);
        }
        Push(player, seconds);
        foreach (var mob in _mobs)
            if (mob.Gone) Died.Add(mob);
        _mobs.RemoveAll(m => m.Gone);
    }

    private static bool Around(VoxelWorld world, int cx, int cz)
    {
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (!world.HasColumn(cx + dx, cz + dz)) return false;
        return true;
    }

    // Each pair of overlapping boxes is pushed apart along x and z, and a mob the player overlaps
    // away from the player.
    private void Push(Body player, float seconds)
    {
        var strength = MathF.Min(1, 8 * seconds);
        for (int i = 0; i < _mobs.Count; i++)
        {
            var a = _mobs[i].Body;
            for (int j = i + 1; j < _mobs.Count; j++)
            {
                var b = _mobs[j].Body;
                if (Apart(a, b) is not { } push) continue;
                a.Velocity -= push * strength * 4;
                b.Velocity += push * strength * 4;
            }
            if (Apart(player, a) is { } away) a.Velocity += away * strength * 6;
        }
    }

    // The way from a box to another along x and z, as long as they overlap, or null where they do not.
    private static Vector3? Apart(Body a, Body b)
    {
        Vector3 aMin = a.Min, aMax = a.Max, bMin = b.Min, bMax = b.Max;
        if (aMin.X >= bMax.X || aMax.X <= bMin.X || aMin.Z >= bMax.Z || aMax.Z <= bMin.Z || aMin.Y >= bMax.Y || aMax.Y <= bMin.Y) return null;
        var way = b.Position - a.Position;
        way.Y = 0;
        var overlap = MathF.Min(MathF.Min(aMax.X - bMin.X, bMax.X - aMin.X), MathF.Min(aMax.Z - bMin.Z, bMax.Z - aMin.Z));
        return way.LengthSquared() > 1e-6f ? Vector3.Normalize(way) * overlap : new Vector3(overlap, 0, 0);
    }

    /// <summary>The nearest living mob a ray from a point meets within a reach, and how far along the ray.</summary>
    public (Mob Mob, float Distance)? Cast(Vector3 from, Vector3 direction, float reach)
    {
        direction = Vector3.Normalize(direction);
        (Mob, float)? nearest = null;
        foreach (var mob in _mobs)
        {
            if (mob.Dying >= 0) continue;
            if (Meets(from, direction, mob.Body.Min, mob.Body.Max) is { } distance && distance <= reach && (nearest is null || distance < nearest.Value.Item2))
                nearest = (mob, distance);
        }
        return nearest;
    }

    // How far along a ray it enters a box, or null where it misses, by the slabs between the box's faces.
    private static float? Meets(Vector3 from, Vector3 direction, Vector3 min, Vector3 max)
    {
        float near = 0, far = float.MaxValue;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = from[axis], d = direction[axis], lo = min[axis], hi = max[axis];
            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < lo || o > hi) return null;
                continue;
            }
            float t0 = (lo - o) / d, t1 = (hi - o) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);
            near = MathF.Max(near, t0);
            far = MathF.Min(far, t1);
            if (near > far) return null;
        }
        return near;
    }

    /// <summary>Whether a living mob's box takes up any of the boxes a block would stand in at a place, which the block is not placed into.</summary>
    public bool Overlaps(int x, int y, int z, IReadOnlyList<Box> boxes)
    {
        foreach (var mob in _mobs)
            if (mob.Dying < 0 && mob.Body.Overlaps(x, y, z, boxes)) return true;
        return false;
    }

    /// <summary>Takes away every mob of a kind, or every mob where none is given, from the loaded columns.</summary>
    public int Clear(MobKind? kind) => _mobs.RemoveAll(m => kind is null || m.Kind == kind);
}
