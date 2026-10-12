using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public class VitalsTests
{
    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    private const float Step = 1 / 60f;

    private static void Run(Vitals vitals, VoxelWorld world, Body body, float seconds, float eyes = 1.62f)
    {
        for (float t = 0; t < seconds; t += Step)
        {
            body.Velocity.Y = MathF.Max(body.Velocity.Y - 32 * Step, -78);
            var middle = body.Position + new Vector3(0, 0.9f, 0);
            var swimming = Blocks.IsWater(world.GetBlock((int)MathF.Floor(middle.X), (int)MathF.Floor(middle.Y), (int)MathF.Floor(middle.Z)));
            if (swimming) body.Velocity.Y = MathF.Max(body.Velocity.Y, -3);
            body.Move(world, body.Velocity * Step, keepToEdges: false);
            vitals.Update(world, body, body.Position + new Vector3(0, eyes, 0), swimming, flying: false, sprinting: false, Step);
        }
    }

    [Fact]
    public void A_Fall_Of_Ten_Blocks_Takes_Seven_Points_A_Fifth_Of_That_Onto_Hay_And_None_Into_Water()
    {
        var world = Floor();
        world.SetBlock(8, 10, 4, BlockId.HayBale);
        for (int y = 9; y <= 10; y++) world.SetBlock(12, y, 4, BlockId.Water);

        var (onStone, onHay, intoWater) = (new Vitals(), new Vitals(), new Vitals());
        Run(onStone, world, new Body { Position = new Vector3(4.5f, 21, 4.5f) }, 3);
        Run(onHay, world, new Body { Position = new Vector3(8.5f, 21, 4.5f) }, 3);
        Run(intoWater, world, new Body { Position = new Vector3(12.5f, 21, 4.5f) }, 3);

        Assert.Equal(Vitals.Most - 7, onStone.Health);
        Assert.Equal(Vitals.Most - 1, onHay.Health);
        Assert.Equal(Vitals.Most, intoWater.Health);
    }

    [Fact]
    public void A_Teleport_Down_Is_No_Fall()
    {
        var world = Floor();
        var vitals = new Vitals();
        var body = new Body { Position = new Vector3(4.5f, 11, 4.5f) };
        Run(vitals, world, body, 0.2f);

        body.Position = new Vector3(4.5f, 11.5f, 8.5f);
        vitals.Arrived();
        body.Position = new Vector3(4.5f, 40, 8.5f);
        vitals.Arrived();
        body.Position = new Vector3(4.5f, 11, 8.5f);
        vitals.Arrived();
        Run(vitals, world, body, 0.5f);

        Assert.Equal(Vitals.Most, vitals.Health);
    }

    [Fact]
    public void Exhaustion_Spends_Saturation_Before_Food()
    {
        var vitals = new Vitals();
        vitals.Set(Vitals.Most, 20, 1);
        var world = Floor();
        var body = new Body { Position = new Vector3(4.5f, 11, 4.5f) };

        vitals.Exhaust(4.5f);
        Run(vitals, world, body, Step);
        Assert.Equal((20, 0f), (vitals.Food, vitals.Saturation));

        vitals.Exhaust(8);
        Run(vitals, world, body, Step);
        Assert.Equal(18, vitals.Food);
    }

    [Fact]
    public void A_Fed_Player_Heals_A_Point_Every_Four_Seconds_And_A_Starving_One_Loses_One_Down_To_One()
    {
        var world = Floor();
        var fed = new Vitals();
        fed.Set(10, 20, 20);
        var starving = new Vitals();
        starving.Set(3, 0, 0);

        Run(fed, world, new Body { Position = new Vector3(4.5f, 11, 4.5f) }, 8.1f);
        Run(starving, world, new Body { Position = new Vector3(8.5f, 11, 4.5f) }, 30);

        Assert.Equal(12, fed.Health);
        Assert.True(fed.Exhaustion > 0 || fed.Saturation < 20, "healing tires the player");
        Assert.Equal(1, starving.Health);
        Assert.False(starving.Dead);
    }

    [Fact]
    public void Under_Water_The_Breath_Lasts_Fifteen_Seconds_And_Then_Drowning_Takes_Two_Points_A_Second()
    {
        var world = Floor();
        for (int y = 11; y <= 14; y++) world.SetBlock(4, y, 4, BlockId.Water);
        var vitals = new Vitals();
        var body = new Body { Position = new Vector3(4.5f, 11, 4.5f) };

        Run(vitals, world, body, 14.5f);
        Assert.Equal(Vitals.Most, vitals.Health);
        Run(vitals, world, body, 2.6f);
        Assert.InRange(vitals.Health, Vitals.Most - 6, Vitals.Most - 2);
    }

    [Fact]
    public void Food_Is_Eaten_While_The_Player_Is_Hungry_And_A_Second_Hit_At_Once_Does_Nothing()
    {
        var vitals = new Vitals();
        vitals.Set(Vitals.Most, 10, 0);

        Assert.True(vitals.Eat(Items.Bread));
        Assert.Equal((15, 6f), (vitals.Food, vitals.Saturation));
        Assert.False(vitals.Eat(Items.Stick));
        vitals.Set(Vitals.Most, Vitals.Most, 5);
        Assert.False(vitals.Eat(Items.Apple));

        Assert.True(vitals.Damage(19, "were hurt"));
        Assert.False(vitals.Damage(5, "were hurt again"));
        Assert.Equal(1, vitals.Health);
        Assert.False(vitals.MaySprint && vitals.Food <= 6);
    }
}
