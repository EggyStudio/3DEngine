using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;

namespace Engine.Tests.Physics;

public partial class PhysicsDeterminismTests
{
    private const string StepsChild = "PHYSICS_STEPS_CHILD";

    private sealed class Block
    {
        public required PhysicsBody Body;
        public float Damage;
        public bool Gone;
    }

    // A castle's wall of 720 welded blocks, the lowest four rows' every fifth a column's hull, on a
    // kinematic foundation, asleep by the time a stone of 60 kg is thrown into it, which wears the
    // blocks it presses by their contact's push, and two bursts that push the blocks they reach at
    // their side facing the burst and crumble the most worn, each weld broken where it holds over
    // 1,500 N. Every body's pose is hashed after the physics' step and again after the welds and
    // the wear, so two runs part at a step and a stage.
    private static List<string> Siege()
    {
        using var world = new PhysicsWorld(new PhysicsSettings());
        world.CreateStaticBox(new Vector3(0, -1, 0), new Vector3(60, 0.5f, 60));
        world.CreateKinematicBox(new Vector3(0, -0.25f, 0.25f), new Vector3(9, 0.25f, 2));
        var column = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            var a = i * MathF.PI / 4;
            column.Add(new Vector3(MathF.Cos(a) * 0.24f, 0, MathF.Sin(a) * 0.24f));
            column.Add(new Vector3(MathF.Cos(a) * 0.24f, 0.48f, MathF.Sin(a) * 0.24f));
        }
        const int across = 30, high = 12, deep = 2;
        var blocks = new Block[across, high, deep];
        for (int z = 0; z < deep; z++)
            for (int y = 0; y < high; y++)
                for (int x = 0; x < across; x++)
                {
                    var at = new Vector3(x * 0.5f - 7.25f, 0.25f + y * 0.5f, z * 0.5f);
                    blocks[x, y, z] = new Block
                    {
                        Body = x % 5 == 0 && y < 4 ? world.CreateConvexHull(at with { Y = at.Y - 0.24f }, [.. column], 2) : world.CreateBox(at, new Vector3(0.24f), 2),
                    };
                }
        var welds = new List<(PhysicsJoint Joint, Block A, Block B)>();
        void Weld(Block a, Block b) => welds.Add((world.CreateWeldJoint(a.Body, b.Body), a, b));
        for (int z = 0; z < deep; z++)
            for (int y = 0; y < high; y++)
                for (int x = 0; x < across; x++)
                {
                    if (x + 1 < across) Weld(blocks[x, y, z], blocks[x + 1, y, z]);
                    if (y + 1 < high) Weld(blocks[x, y, z], blocks[x, y + 1, z]);
                    if (z + 1 < deep) Weld(blocks[x, y, z], blocks[x, y, z + 1]);
                }
        var all = blocks.Cast<Block>().ToList();
        var of = all.ToDictionary(b => b.Body);
        void Crumble(Block block)
        {
            if (block.Gone) return;
            block.Gone = true;
            for (int i = welds.Count - 1; i >= 0; i--)
                if (welds[i].A == block || welds[i].B == block)
                {
                    world.DestroyJoint(welds[i].Joint);
                    welds.RemoveAt(i);
                }
            world.Destroy(block.Body);
        }
        PhysicsBody? stone = null;
        var touching = new HashSet<Block>();
        var hashes = new List<string>();
        string Hash(int step, string stage)
        {
            var bytes = new List<byte>();
            foreach (var block in all.Where(b => !b.Gone))
            {
                var (p, q) = (world.GetPosition(block.Body), world.GetRotation(block.Body));
                foreach (var f in new[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z, q.W })
                    bytes.AddRange(BitConverter.GetBytes(f));
            }
            return $"step {step} {stage} {Convert.ToHexString(SHA256.HashData(bytes.ToArray()))[..16]}";
        }
        for (int step = 0; step < 720; step++)
        {
            if (step == 240)
            {
                stone = world.CreateConvexHull(new Vector3(-1, 3, -8), [.. column.Select(p => p * 2.5f)], 60);
                world.SetLinearVelocity(stone.Value, new Vector3(0.5f, 2, 22));
            }
            if (step == 360 || step == 480)
            {
                var center = step == 360 ? new Vector3(-0.5f, 2.2f, -0.6f) : new Vector3(2.5f, 1.1f, 1.2f);
                foreach (var body in world.Overlap(center, 4).OrderBy(b => (b.Kind, b.Handle)))
                {
                    if (!of.TryGetValue(body, out var block) || block.Gone) continue;
                    var away = world.GetPosition(body) - center;
                    var push = 300 * (1 - away.Length() / 4);
                    if (push <= 0) continue;
                    var way = away.Length() > 0.01f ? Vector3.Normalize(away) : Vector3.UnitY;
                    world.ApplyImpulseAt(body, way * push, world.GetPosition(body) - way * 0.4f);
                    block.Damage += push;
                    if (block.Damage > 250) Crumble(block);
                }
            }
            world.StepOnce(1 / 60f);
            hashes.Add(Hash(step, "after the physics' step"));
            var (started, ended) = world.TakePendingContacts();
            if (stone is { } s)
            {
                foreach (var contact in started)
                    if ((contact.BodyA == s ? contact.BodyB : contact.BodyB == s ? contact.BodyA : (PhysicsBody?)null) is { } other && of.TryGetValue(other, out var block))
                        touching.Add(block);
                foreach (var contact in ended)
                    touching.RemoveWhere(b => b.Body == contact.BodyA || b.Body == contact.BodyB);
                // Worn in the wall's own order, since a set gives its blocks in the order of their
                // places in memory.
                foreach (var block in all.Where(touching.Contains).ToArray())
                {
                    if (block.Gone) continue;
                    block.Damage += world.GetContactImpulse(s, block.Body);
                    if (block.Damage > 250) Crumble(block);
                }
                touching.RemoveWhere(b => b.Gone);
            }
            for (int i = welds.Count - 1; i >= 0; i--)
                if (world.GetJointForce(welds[i].Joint) > 1500)
                {
                    world.DestroyJoint(welds[i].Joint);
                    welds.RemoveAt(i);
                }
            hashes.Add(Hash(step, "after the welds and the wear"));
        }
        return hashes;
    }

    // The first of two runs' hashes that differ, or null where every one agrees.
    private static string? FirstApart(List<string> a, List<string> b) =>
        a.Zip(b).Where(p => p.First != p.Second).Select(p => $"{p.First} where the other run reads {p.Second.Split(' ')[^1]}").FirstOrDefault()
        ?? (a.Count == b.Count ? null : $"{a.Count} hashes where the other run has {b.Count}");

    [Fact]
    public async Task A_Welded_Wall_A_Stone_And_Two_Bursts_Step_The_Same_In_One_Process_And_Across_Two()
    {
        var first = Siege();
        if (Environment.GetEnvironmentVariable(StepsChild) is { } written)
        {
            File.WriteAllLines(written, first);
            return;
        }
        FirstApart(first, Siege()).Should().BeNull("the same wall stepped twice in one process steps alike, to the bit, after every step and stage");

        // The same run in a process of its own, which holds no state this one holds.
        using var folder = new TestFolder("engine-physics-steps-");
        var steps = Path.Combine(folder.Path, "steps.txt");
        var host = Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet" ? Environment.ProcessPath! : "dotnet";
        var start = new ProcessStartInfo(host) { WorkingDirectory = folder.Path, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "test", typeof(PhysicsDeterminismTests).Assembly.Location, "--filter", $"FullyQualifiedName={typeof(PhysicsDeterminismTests).FullName}.{nameof(A_Welded_Wall_A_Stone_And_Two_Bursts_Step_The_Same_In_One_Process_And_Across_Two)}" })
            start.ArgumentList.Add(argument);
        start.Environment[StepsChild] = steps;
        using var child = Process.Start(start)!;
        using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var said = child.StandardOutput.ReadToEndAsync(bound.Token);
        var complained = child.StandardError.ReadToEndAsync(bound.Token);
        await child.WaitForExitAsync(bound.Token);
        File.Exists(steps).Should().BeTrue($"the process of its own wrote its steps, and said {await said}{await complained}");
        FirstApart(first, [.. File.ReadAllLines(steps)]).Should().BeNull("the same wall stepped in another process steps alike, to the bit, after every step and stage");
    }
}
