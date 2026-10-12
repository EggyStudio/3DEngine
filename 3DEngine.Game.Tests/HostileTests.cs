using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public class HostileTests
{
    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    private const float Step = 1 / 30f;

    private static Mob Spawn(MobKind kind, float x, float z) => new(1, new MobRecord(kind, 0, new Vector3(x, 11, z), 0, kind.Health, null), 1);

    // Runs a mob for so many seconds hunting a player standing at a place, and gives what it did.
    private static List<MobEvent> Hunt(Mob mob, VoxelWorld world, Body player, float hour, float seconds)
    {
        var events = new List<MobEvent>();
        for (float t = 0; t < seconds; t += Step)
            mob.Update(world, new MobTarget(player, player.Position + new Vector3(0, 1.62f, 0)), player.Position, hour, Step, events);
        return events;
    }

    [Fact]
    public void A_Hostile_Mob_Spawns_Under_The_Night_Sky_And_Not_By_Day_Nor_By_A_Lamp()
    {
        var world = Floor();
        world.SetBlock(20, 11, 20, BlockId.Glowstone);

        Assert.True(Mobs.Spawnable(world, MobKinds.Zombie, 4, 11, 4, hour: 0, roll: 7));
        Assert.False(Mobs.Spawnable(world, MobKinds.Zombie, 4, 11, 4, hour: 0, roll: 0));
        Assert.False(Mobs.Spawnable(world, MobKinds.Zombie, 4, 11, 4, hour: 12, roll: 7));
        Assert.False(Mobs.Spawnable(world, MobKinds.Zombie, 21, 11, 20, hour: 0, roll: 7));
        Assert.Equal((0, 11), (Mobs.Darkening(12), Mobs.Darkening(0)));
    }

    [Fact]
    public void A_Zombie_Runs_At_The_Player_It_Sees_And_Strikes_Once_A_Second()
    {
        var world = Floor();
        var zombie = Spawn(MobKinds.Zombie, 4.5f, 4.5f);
        var player = new Body { Position = new Vector3(12.5f, 11, 4.5f) };

        var events = Hunt(zombie, world, player, hour: 0, seconds: 8);

        var strikes = events.OfType<MobStrike>().ToList();
        Assert.InRange(strikes.Count, 3, 8);
        Assert.All(strikes, s => Assert.Equal(3, s.Damage));
        Assert.Equal("hunting", zombie.Doing);
    }

    [Fact]
    public void A_Creeper_Explodes_Beside_The_Player_And_Gives_Up_When_The_Player_Gets_Away()
    {
        var world = Floor();
        var creeper = Spawn(MobKinds.Creeper, 4.5f, 4.5f);
        var player = new Body { Position = new Vector3(6.5f, 11, 4.5f) };

        Hunt(creeper, world, player, hour: 0, seconds: 0.5f);
        Assert.True(creeper.Fuse > 0);
        player.Position = new Vector3(14.5f, 11, 4.5f);
        Assert.Empty(Hunt(creeper, world, player, hour: 0, seconds: 0.5f).OfType<MobBlast>());
        Assert.True(creeper.Fuse < 0);

        var stays = Spawn(MobKinds.Creeper, 4.5f, 8.5f);
        var blast = Assert.Single(Hunt(stays, world, new Body { Position = new Vector3(6.5f, 11, 8.5f) }, hour: 0, seconds: 3).OfType<MobBlast>());
        Assert.Equal(3, blast.Power);
        Assert.True(stays.Exploded && stays.Gone);
        Assert.Empty(Loot.Of(stays, new Random(1)));
    }

    [Fact]
    public void A_Skeleton_Shoots_From_Range_And_Its_Arrows_Strike_The_Player()
    {
        var world = Floor();
        var skeleton = Spawn(MobKinds.Skeleton, 4.5f, 4.5f);
        var player = new Body { Position = new Vector3(4.5f, 11, 12.5f) };

        var shots = Hunt(skeleton, world, player, hour: 0, seconds: 5).OfType<MobShot>().ToList();
        Assert.InRange(shots.Count, 2, 3);

        var arrows = new Arrows();
        arrows.Shoot(shots[0].From, shots[0].Velocity);
        var hits = new List<(int Damage, Vector3 From)>();
        for (float t = 0; t < 2; t += Step) hits.AddRange(arrows.Update(world, player, Step));
        var hit = Assert.Single(hits);
        Assert.InRange(hit.Damage, 3, 4);
    }

    [Fact]
    public void An_Explosion_Hollows_Dirt_Takes_Less_Stone_And_Nothing_Under_Water()
    {
        var dirt = Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Dirt));
        var stone = Floor();
        var flooded = Laid.World(c => { Laid.Fill(c, 0, 10, BlockId.Dirt); Laid.Fill(c, 11, 13, BlockId.Water); });
        var at = new Vector3(8.5f, 11.5f, 8.5f);

        var inDirt = Explosion.Breaks(dirt, at, 3, new Random(1)).Count;
        var inStone = Explosion.Breaks(stone, at, 3, new Random(1)).Count;
        var underWater = Explosion.Breaks(flooded, at, 3, new Random(1)).Count;

        Assert.True(inDirt > 20, $"{inDirt} dirt broken");
        Assert.True(inStone < inDirt / 2, $"{inStone} stone against {inDirt} dirt");
        Assert.Equal(0, underWater);
        Assert.Equal(43, Explosion.Damage(0, 3, 1));
        Assert.Equal(0, Explosion.Damage(6, 3, 1));
    }

    [Fact]
    public void A_Zombie_Burns_Under_The_Day_Sky_And_Not_At_Night()
    {
        var world = Floor();
        var byDay = Spawn(MobKinds.Zombie, 4.5f, 4.5f);
        var byNight = Spawn(MobKinds.Zombie, 8.5f, 8.5f);

        for (float t = 0; t < 3.05f; t += Step)
        {
            byDay.Update(world, null, Vector3.Zero, 12, Step, []);
            byNight.Update(world, null, Vector3.Zero, 0, Step, []);
        }

        Assert.Equal(MobKinds.Zombie.Health - 3, byDay.Health);
        Assert.Equal(MobKinds.Zombie.Health, byNight.Health);
    }

    [Fact]
    public void A_Column_Keeps_Its_Animals_And_Lets_Its_Hostile_Mobs_Go()
    {
        var mobs = new Mobs();
        var column = new ChunkColumn(0, 0);
        column.Spawns.Add(new MobRecord(MobKinds.Cow, 0, new Vector3(4.5f, 11, 4.5f), 0, 10, null));
        mobs.Joined(column);
        mobs.Add(new MobRecord(MobKinds.Zombie, 0, new Vector3(6.5f, 11, 6.5f), 0, 20, null));

        mobs.Leaving(0, 0);

        Assert.Equal(1, mobs.Kept);
        Assert.DoesNotContain(mobs.Columns().SelectMany(c => c.Mobs), m => m.Kind.Hostile);
    }
}
