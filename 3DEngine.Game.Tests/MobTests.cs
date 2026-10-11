using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public sealed class MobTests : IDisposable
{
    // A folder of this test's own, removed with it, so no run reads another's save.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "3DEngine.Game.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    // A floor with a room of stone on it from 4 to 10 along x and z, its door in the north wall at x 7.
    private static VoxelWorld Room()
    {
        var world = Floor();
        for (int y = 11; y <= 14; y++)
            for (int z = 4; z <= 10; z++)
                for (int x = 4; x <= 10; x++)
                {
                    var wall = x == 4 || x == 10 || z == 4 || z == 10 || y == 14;
                    world.SetBlock(x, y, z, wall ? BlockId.Stone : BlockId.Air);
                }
        world.SetBlock(7, 11, 4, BlockId.OakDoorSouth);
        world.SetBlock(7, 12, 4, BlockId.OakDoorSouthTop);
        return world;
    }

    private static MobRecord At(MobKind kind, float x, float y, float z, Vector3? home = null) => new(kind, 0, new Vector3(x, y, z), 0, kind.Health, home);

    private static void Run(Mob mob, VoxelWorld world, float hour, float seconds)
    {
        for (float t = 0; t < seconds; t += 1 / 30f) mob.Update(world, new Vector3(100, 100, 100), hour, 1 / 30f);
    }

    [Fact]
    public void A_Way_Goes_Around_A_Wall_And_Through_None_Of_It()
    {
        var world = Floor();
        for (int z = 0; z <= 10; z++)
            for (int y = 11; y <= 12; y++) world.SetBlock(8, y, z, BlockId.Stone);

        var path = Pathfinder.Find(world, MobKinds.Pig, new Step(4, 11, 5), new Step(12, 11, 5));

        Assert.NotNull(path);
        Assert.Equal(new Step(12, 11, 5), path[^1]);
        Assert.DoesNotContain(path, s => s.X == 8 && s.Z is >= 0 and <= 10);
        Assert.Contains(path, s => s.Z is < 0 or > 10);
    }

    [Fact]
    public void A_Way_Climbs_One_Block_And_Not_Two()
    {
        var world = Floor();
        world.SetBlock(6, 11, 5, BlockId.Stone);
        world.SetBlock(6, 11, 9, BlockId.Stone);
        world.SetBlock(6, 12, 9, BlockId.Stone);

        Assert.NotNull(Pathfinder.Find(world, MobKinds.Pig, new Step(4, 11, 5), new Step(6, 12, 5)));
        Assert.Null(Pathfinder.Find(world, MobKinds.Pig, new Step(4, 11, 9), new Step(6, 13, 9)));
    }

    [Fact]
    public void A_Shut_Door_Is_A_Way_For_A_Villager_And_Not_For_A_Pig()
    {
        var world = Room();

        var villager = Pathfinder.Find(world, MobKinds.Villager, new Step(7, 11, 1), new Step(7, 11, 7));

        Assert.NotNull(villager);
        Assert.Contains(new Step(7, 11, 4), villager);
        Assert.Null(Pathfinder.Find(world, MobKinds.Pig, new Step(7, 11, 1), new Step(7, 11, 7)));
    }

    [Fact]
    public void A_Mob_Falls_Onto_The_Floor_And_Stands_On_It()
    {
        var world = Floor();
        var mob = new Mob(1, At(MobKinds.Cow, 4.5f, 16, 4.5f), 1);

        Run(mob, world, 12, 2);

        Assert.Equal(11, mob.Body.Position.Y, 3);
        Assert.True(mob.Body.OnGround);
    }

    [Fact]
    public void A_Hit_Takes_Health_And_Another_While_The_Mob_Is_Red_Does_Nothing()
    {
        var world = Floor();
        var mob = new Mob(1, At(MobKinds.Chicken, 4.5f, 11, 4.5f), 1);
        var from = new Vector3(2.5f, 11, 4.5f);

        Assert.True(mob.Hit(from, 1));
        Assert.False(mob.Hit(from, 1));
        Assert.Equal(MobKinds.Chicken.Health - 1, mob.Health);
        // Knocked away from the hit, along x.
        Assert.True(mob.Body.Velocity.X > 0);

        for (int hit = 1; hit < MobKinds.Chicken.Health; hit++)
        {
            Run(mob, world, 12, 0.6f);
            Assert.True(mob.Hit(mob.Body.Position - Vector3.UnitX, 1));
        }
        Assert.True(mob.Dying >= 0);
        Run(mob, world, 12, 1.1f);
        Assert.True(mob.Gone);
    }

    [Fact]
    public void A_Villager_Goes_Home_At_Night_Through_Its_Door_And_Shuts_It_Behind_It()
    {
        var world = Room();
        var home = new Vector3(7.5f, 11, 7.5f);
        var villager = new Mob(1, At(MobKinds.Villager, 7.5f, 11, 1.5f, home), 1);

        Run(villager, world, 20, 30);

        // Inside the room, where it wanders within two blocks of its home through the night.
        var at = villager.Body.Position;
        Assert.True(at.X is > 5 and < 10 && at.Z is > 5 and < 10, $"the villager stands at {at}");
        Assert.True(Pathfinder.IsShutDoor(world.GetBlock(7, 11, 4)), $"the door is {Blocks.Get(world.GetBlock(7, 11, 4)).Key}");
    }

    [Fact]
    public void A_Column_Places_Its_Mobs_Once_And_Has_Them_Back_Where_They_Were_Left()
    {
        var mobs = new Mobs();
        var spawn = At(MobKinds.Sheep, 4.5f, 11, 4.5f);
        var column = new ChunkColumn(0, 0);
        column.Spawns.Add(spawn);
        mobs.Joined(column);
        mobs.All[0].Body.Position = new Vector3(9.5f, 11, 6.5f);

        mobs.Leaving(0, 0);
        Assert.Empty(mobs.All);
        Assert.Equal(1, mobs.Kept);

        // Generated again as it loads, with the same mob to place, which is not placed twice.
        var again = new ChunkColumn(0, 0);
        again.Spawns.Add(spawn);
        mobs.Joined(again);

        var back = Assert.Single(mobs.All);
        Assert.Equal(new Vector3(9.5f, 11, 6.5f), back.Body.Position);
        Assert.Equal(0, mobs.Kept);
    }

    [Fact]
    public void Mobs_Read_Back_From_The_Save_With_The_Columns_That_Hold_None()
    {
        var mobs = new Mobs();
        var villager = new MobRecord(MobKinds.Villager, 2, new Vector3(-20.5f, 64, 40.25f), 1.5f, 13, new Vector3(-18.5f, 64, 42.5f));
        var sheep = new MobRecord(MobKinds.Sheep, 3, new Vector3(-30.5f, 70, 33.5f), -2, 8, null);
        var full = new ChunkColumn(-2, 2);
        full.Spawns.AddRange([villager, sheep]);
        mobs.Joined(full);
        mobs.Joined(new ChunkColumn(5, -7));
        var save = new WorldSave(_folder);

        save.WriteMobs(mobs.Columns());
        var (kept, placed) = new WorldSave(_folder).ReadMobs();

        Assert.Equal([(-2, 2), (5, -7)], placed.Order());
        Assert.Equal([villager, sheep], kept[(-2, 2)]);
        Assert.False(kept.ContainsKey((5, -7)));
    }

    [Fact]
    public void Herds_Are_Placed_The_Same_Every_Time_On_Open_Ground()
    {
        var land = new Overworld(1);
        var herds = 0;
        for (int cz = -6; cz <= 6; cz++)
            for (int cx = -6; cx <= 6; cx++)
            {
                var column = land.Generate(cx, cz);
                if (column.Spawns.Count == 0) continue;
                herds++;
                Assert.Equal(column.Spawns, land.Generate(cx, cz).Spawns);
                foreach (var mob in column.Spawns)
                {
                    int x = (int)MathF.Floor(mob.Position.X) & Section.Mask, y = (int)mob.Position.Y, z = (int)MathF.Floor(mob.Position.Z) & Section.Mask;
                    Assert.True(Blocks.Collides(column.Get(x, y - 1, z)), $"a {mob.Kind.Name} stands on {Blocks.Get(column.Get(x, y - 1, z)).Key}");
                    Assert.Equal(BlockId.Air, column.Get(x, y, z));
                    Assert.Equal(BlockId.Air, column.Get(x, y + 1, z));
                }
            }
        Assert.True(herds >= 5, $"{herds} of 169 columns hold a herd");
    }

    [Fact]
    public void A_Village_Places_Its_Villagers_Once_Each_With_A_Home_To_Stand_In()
    {
        var land = new Overworld(1);
        var village = Structures.Find("village")!;
        var start = Enumerable.Range(-6, 13).SelectMany(cz => Enumerable.Range(-6, 13).Select(cx => village.StartIn(cx, cz, land)))
            .First(s => s is not null)!.Value;

        var columns = new Dictionary<(int, int), ChunkColumn>();
        for (int cz = (start.Z - 4) >> Section.Shift; cz <= (start.Z + village.Size + 4) >> Section.Shift; cz++)
            for (int cx = (start.X - 4) >> Section.Shift; cx <= (start.X + village.Size + 4) >> Section.Shift; cx++)
                columns[(cx, cz)] = land.Generate(cx, cz);
        BlockId Block(int x, int y, int z) =>
            columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var c) && (uint)y < ChunkColumn.Height ? c.Get(x & Section.Mask, y, z & Section.Mask) : BlockId.Air;

        var villagers = columns.Values.SelectMany(c => c.Spawns).Where(m => m.Kind == MobKinds.Villager).ToList();
        Assert.True(villagers.Count >= 4, $"the village has {villagers.Count} villagers");
        Assert.Equal(villagers.Count, villagers.Select(v => v.Position).Distinct().Count());
        foreach (var villager in villagers)
        {
            var home = villager.Home!.Value;
            int x = (int)MathF.Floor(home.X), y = (int)home.Y, z = (int)MathF.Floor(home.Z);
            Assert.True(Blocks.Collides(Block(x, y - 1, z)) && Block(x, y, z) == BlockId.Air && Block(x, y + 1, z) == BlockId.Air,
                $"a villager's home at {x}, {y}, {z} stands on {Blocks.Get(Block(x, y - 1, z)).Key} under {Blocks.Get(Block(x, y, z)).Key}");
        }
    }
}
