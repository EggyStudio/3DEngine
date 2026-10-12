namespace Engine.Game;

/// <summary>
/// The furnaces of a world by the place of their block, each burning and smelting while its column
/// is loaded, its block lit while its fire burns and unlit when it goes out, and giving up what it
/// holds once its block is gone, broken or blown up.
/// </summary>
/// <remarks>A save keeps every furnace, those of columns not loaded among them, in the world's <c>furnaces.json</c>.</remarks>
public sealed class Furnaces
{
    private readonly Dictionary<(int X, int Y, int Z), Furnace> _all = [];

    public IReadOnlyDictionary<(int X, int Y, int Z), Furnace> All => _all;

    /// <summary>The furnace of the block at a place, begun empty the first time it is asked for.</summary>
    public Furnace At(int x, int y, int z)
    {
        if (!_all.TryGetValue((x, y, z), out var furnace)) _all[(x, y, z)] = furnace = new Furnace();
        return furnace;
    }

    public void Add((int X, int Y, int Z) at, Furnace furnace) => _all[at] = furnace;

    public void Clear() => _all.Clear();

    public static bool IsFurnace(BlockId block) => block is BlockId.Furnace or BlockId.FurnaceLit;

    /// <summary>
    /// Burns and smelts every furnace whose column is loaded, lights or puts out its block as its
    /// fire burns, and takes away those whose block is gone.
    /// </summary>
    /// <returns>What the furnaces whose block is gone held, and where, for the caller to drop.</returns>
    public List<((int X, int Y, int Z) At, ItemStack Stack)> Update(VoxelWorld world, float seconds)
    {
        var spilled = new List<((int, int, int), ItemStack)>();
        foreach (var (at, furnace) in _all.ToList())
        {
            if (!world.HasColumn(VoxelWorld.ColumnOf(at.X), VoxelWorld.ColumnOf(at.Z))) continue;
            var block = world.GetBlock(at.X, at.Y, at.Z);
            if (!IsFurnace(block))
            {
                foreach (var stack in furnace.Slots)
                    if (stack is { } held) spilled.Add((at, held));
                _all.Remove(at);
                continue;
            }
            furnace.Update(seconds);
            var lit = furnace.Lit ? BlockId.FurnaceLit : BlockId.Furnace;
            if (block != lit) world.SetBlock(at.X, at.Y, at.Z, lit);
        }
        return spilled;
    }
}
