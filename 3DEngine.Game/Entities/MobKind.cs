using System.Numerics;

namespace Engine.Game;

/// <summary>A box of a mob's model between two corners in sixteenths of a block, its color, or null for the color its mob varies by.</summary>
public readonly record struct ModelBox(Vector3 Min, Vector3 Max, Color? Color);

/// <summary>
/// How a piece of a model moves: not at all, with the head's look, as one of the two pairs of legs,
/// as a wing, as one of two arms swinging against the legs, or as one of a spider's two sets of
/// legs, which swing about the vertical.
/// </summary>
public enum Swing
{
    None,
    Head,
    LegA,
    LegB,
    LeftWing,
    RightWing,
    ArmA,
    ArmB,
    SpiderA,
    SpiderB,
}

/// <summary>A piece of a mob's model that moves as one, turning about its pivot, in sixteenths of a block.</summary>
public sealed record ModelPart(Swing Swing, Vector3 Pivot, ModelBox[] Boxes)
{
    /// <summary>How the piece is turned about its pivot before it swings, in radians about x, then z, then y, as a zombie holds its arms out.</summary>
    public Vector3 Pose { get; init; }

    /// <summary>Whether a box of it takes its mob's own color, so it is meshed once for each.</summary>
    public bool Varies => Boxes.Any(b => b.Color is null);
}

/// <summary>
/// A kind of mob: its box, how fast it walks, its health, the colors it varies by, and its model,
/// facing north (toward -z) with its feet's middle at the origin.
/// </summary>
/// <param name="Name">The name <c>voxel.spawn</c> takes and a save keeps.</param>
/// <param name="HalfWidth">Half its box's width, as Minecraft's sizes give it.</param>
/// <param name="Height">Its box's height.</param>
/// <param name="Speed">How fast it wanders, in blocks a second.</param>
/// <param name="Health">The hits it takes before it dies.</param>
/// <param name="Variants">The colors its model's own boxes take, one a variant, chosen as <see cref="Odds"/> say.</param>
/// <param name="Parts">Its model.</param>
public sealed record MobKind(string Name, float HalfWidth, float Height, float Speed, int Health, Color[] Variants, ModelPart[] Parts)
{
    /// <summary>Its place in <see cref="MobKinds.All"/>.</summary>
    public byte Id { get; init; }

    /// <summary>How often each variant is chosen, in thousandths, or null where each is as likely.</summary>
    public int[]? Odds { get; init; }

    /// <summary>Whether it opens a shut door in its way and shuts it behind it, as a villager does.</summary>
    public bool OpensDoors { get; init; }

    /// <summary>Whether it falls slowly, flapping, as a chicken does.</summary>
    public bool FallsSlowly { get; init; }

    /// <summary>Whether it hunts the player in survival, spawns in the dark and is not kept by a column.</summary>
    public bool Hostile { get; init; }

    /// <summary>The health a hit of its takes from the player, for a mob that strikes.</summary>
    public int AttackDamage { get; init; }

    /// <summary>How fast it runs at the player, in blocks a second.</summary>
    public float ChaseSpeed { get; init; }

    /// <summary>How far off it notices the player it can see.</summary>
    public float FollowRange { get; init; } = 16;

    /// <summary>Whether it shoots arrows rather than striking, as a skeleton does.</summary>
    public bool Shoots { get; init; }

    /// <summary>Whether it hisses and explodes beside the player rather than striking, as a creeper does.</summary>
    public bool Explodes { get; init; }

    /// <summary>Whether it burns under the open sky by day, as zombies and skeletons do.</summary>
    public bool Burns { get; init; }

    /// <summary>The height of its eyes, which turn toward the player, at its head's pivot.</summary>
    public float Eyes => Parts.FirstOrDefault(p => p.Swing == Swing.Head)?.Pivot.Y / 16 ?? Height * 0.85f;

    /// <summary>A variant from a roll of 0 to 999.</summary>
    public byte Variant(int roll)
    {
        if (Odds is null) return (byte)(roll % Variants.Length);
        for (int i = 0; i < Odds.Length; i++)
        {
            if (roll < Odds[i]) return (byte)i;
            roll -= Odds[i];
        }
        return 0;
    }
}

/// <summary>Every kind of mob, by its id, with their models after Minecraft's.</summary>
public static class MobKinds
{
    private static readonly Color Black = new(28, 24, 24), White = new(236, 236, 236);

    // A box between two corners, in sixteenths of a block.
    private static ModelBox B(float x0, float y0, float z0, float x1, float y1, float z1, Color? color) =>
        new(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), color);

    // A pair of eyes on a face at z, each a dark pixel inward of a light one where a light color is given.
    private static IEnumerable<ModelBox> Eyes(float inner, float y, float z, Color pupil, Color? white = null)
    {
        yield return B(-inner - 1, y, z - 0.05f, -inner, y + 1, z, pupil);
        yield return B(inner, y, z - 0.05f, inner + 1, y + 1, z, pupil);
        if (white is not { } light) yield break;
        yield return B(-inner - 2, y, z - 0.05f, -inner - 1, y + 1, z, light);
        yield return B(inner + 1, y, z - 0.05f, inner + 2, y + 1, z, light);
    }

    // Four legs of a size under a body, each turning at its top, the front left and back right as one pair.
    private static ModelPart[] Legs(float x, float zFront, float zBack, float height, float size, Func<float, float, IEnumerable<ModelBox>> boxes) =>
    [
        .. new (float X, float Z, Swing Swing)[] { (-x, zFront, Swing.LegA), (x, zFront, Swing.LegB), (-x, zBack, Swing.LegB), (x, zBack, Swing.LegA) }
            .Select(leg => new ModelPart(leg.Swing, new Vector3(leg.X, height, leg.Z), [.. boxes(leg.X - size / 2, leg.Z - size / 2)])),
    ];

    public static readonly MobKind Pig = MakePig();
    public static readonly MobKind Cow = MakeCow();
    public static readonly MobKind Sheep = MakeSheep();
    public static readonly MobKind Chicken = MakeChicken();
    public static readonly MobKind Villager = MakeVillager();
    public static readonly MobKind Zombie = MakeZombie();
    public static readonly MobKind Skeleton = MakeSkeleton();
    public static readonly MobKind Creeper = MakeCreeper();
    public static readonly MobKind Spider = MakeSpider();

    public static readonly MobKind[] All = [Pig, Cow, Sheep, Chicken, Villager, Zombie, Skeleton, Creeper, Spider];

    /// <summary>The hostile kinds, and how often each of a hundred spawns is of it, as Minecraft's overworld weighs them.</summary>
    public static readonly (MobKind Kind, int Weight)[] Night = [(Zombie, 40), (Skeleton, 30), (Spider, 15), (Creeper, 15)];

    // An arm held out ahead, as a zombie's and a skeleton's are.
    private static readonly Vector3 Ahead = new(MathF.PI / 2, 0, 0);

    public static MobKind? Find(string name) => All.FirstOrDefault(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static MobKind MakePig()
    {
        Color pink = new(236, 158, 160), snout = new(218, 118, 130), hoof = new(196, 120, 124);
        ModelPart[] parts =
        [
            new(Swing.None, Vector3.Zero, [B(-5, 6, -8, 5, 14, 8, pink)]),
            new(Swing.Head, new Vector3(0, 11, -7), [B(-4, 7, -15, 4, 15, -7, pink), B(-2, 8, -16, 2, 11, -15, snout), .. Eyes(2, 11, -15, Black, White)]),
            .. Legs(3, -5, 5, 6, 4, (x, z) => [B(x, 0, z, x + 4, 6, z + 4, pink), B(x - 0.05f, 0, z - 0.05f, x + 4.05f, 1, z + 4.05f, hoof)]),
        ];
        return new MobKind("pig", 0.45f, 0.9f, 1.25f, 10, [pink], parts) { Id = 0 };
    }

    private static MobKind MakeCow()
    {
        Color brown = new(78, 56, 40), patch = new(226, 224, 216), horn = new(214, 204, 178), muzzle = new(196, 160, 146), udder = new(226, 160, 166);
        ModelPart[] parts =
        [
            new(Swing.None, Vector3.Zero,
            [
                B(-6, 12, -9, 6, 22, 9, brown),
                B(6, 14, -4, 6.05f, 20, 3, patch), B(-6.05f, 13, 0, -6, 18, 6, patch), B(-3, 22, -6, 2, 22.05f, 0, patch),
                B(-2, 11, 4, 2, 12, 8, udder),
            ]),
            new(Swing.Head, new Vector3(0, 18, -9),
            [
                B(-4, 15, -15, 4, 23, -9, brown), B(-2, 15, -16, 2, 18, -15, muzzle), B(-2, 18, -15.05f, 2, 22, -15, patch),
                B(-5, 21, -13, -4, 24, -12, horn), B(4, 21, -13, 5, 24, -12, horn), .. Eyes(3, 19, -15, Black),
            ]),
            .. Legs(4, -6, 6, 12, 4, (x, z) => [B(x, 0, z, x + 4, 12, z + 4, brown), B(x - 0.05f, 0, z - 0.05f, x + 4.05f, 4, z + 4.05f, patch)]),
        ];
        return new MobKind("cow", 0.45f, 1.4f, 1.1f, 10, [brown], parts) { Id = 1 };
    }

    private static MobKind MakeSheep()
    {
        Color skin = new(222, 192, 170);
        ModelPart[] parts =
        [
            new(Swing.None, Vector3.Zero, [B(-6, 11, -9, 6, 20, 9, null)]),
            new(Swing.Head, new Vector3(0, 17, -8), [B(-3, 14, -15, 3, 20, -8, skin), B(-3.5f, 17, -14, 3.5f, 21, -7.5f, null), .. Eyes(1, 16, -15, Black)]),
            .. Legs(3, -5, 6, 12, 4, (x, z) => [B(x, 0, z, x + 4, 12, z + 4, skin), B(x - 0.5f, 6, z - 0.5f, x + 4.5f, 12, z + 4.5f, null)]),
        ];
        // White, light gray, gray, black, brown and pink wool, as often as Minecraft's sheep are born each.
        Color[] wool = [new(233, 236, 236), new(142, 142, 135), new(63, 68, 72), new(24, 24, 30), new(114, 71, 40), new(237, 141, 172)];
        return new MobKind("sheep", 0.45f, 1.3f, 1.15f, 8, wool, parts) { Id = 2, Odds = [818, 50, 50, 50, 30, 2] };
    }

    private static MobKind MakeChicken()
    {
        Color feathers = new(240, 240, 238), beak = new(234, 164, 44), wattle = new(198, 34, 36), leg = new(226, 170, 52);
        ModelPart[] parts =
        [
            new(Swing.None, Vector3.Zero, [B(-3, 4, -4, 3, 10, 4, feathers)]),
            new(Swing.Head, new Vector3(0, 9, -4), [B(-2, 9, -6, 2, 15, -3, feathers), B(-2, 11, -8, 2, 13, -6, beak), B(-1, 9, -7, 1, 11, -6, wattle), .. Eyes(1, 13, -6, Black)]),
            new(Swing.LeftWing, new Vector3(-3, 10, 0), [B(-4, 6, -3, -3, 10, 3, feathers)]),
            new(Swing.RightWing, new Vector3(3, 10, 0), [B(3, 6, -3, 4, 10, 3, feathers)]),
            new(Swing.LegA, new Vector3(-1.5f, 4, 0.5f), [B(-2, 0, 0, -1, 4, 1, leg), B(-3, 0, -2, 0, 0.5f, 1, leg)]),
            new(Swing.LegB, new Vector3(1.5f, 4, 0.5f), [B(1, 0, 0, 2, 4, 1, leg), B(0, 0, -2, 3, 0.5f, 1, leg)]),
        ];
        return new MobKind("chicken", 0.2f, 0.7f, 1.0f, 4, [feathers], parts) { Id = 3, FallsSlowly = true };
    }

    private static MobKind MakeVillager()
    {
        Color skin = new(186, 134, 104), nose = new(168, 116, 88), brow = new(66, 48, 38), eye = new(58, 128, 60), pants = new(72, 56, 44);
        ModelPart[] parts =
        [
            new(Swing.LegA, new Vector3(-2, 12, 0), [B(-4, 0, -2, 0, 12, 2, pants)]),
            new(Swing.LegB, new Vector3(2, 12, 0), [B(0, 0, -2, 4, 12, 2, pants)]),
            // The robe down to above the feet, and the arms crossed in front of it.
            new(Swing.None, Vector3.Zero, [B(-4.5f, 5, -3.5f, 4.5f, 24, 3.5f, null), B(-6, 15, -6.5f, 6, 19, -3.5f, null), B(-3, 15.5f, -6.55f, 3, 18.5f, -6.5f, skin)]),
            new(Swing.Head, new Vector3(0, 24, 0),
            [
                B(-4, 24, -4, 4, 34, 4, skin), B(-1, 25, -6, 1, 29, -4, nose), B(-4, 30, -4.05f, 4, 31, -4, brow),
                .. Eyes(1, 29, -4, eye, White),
            ]),
        ];
        // A robe of the plains, the desert, the taiga and the snow, as Minecraft's villagers wear by biome.
        Color[] robes = [new(118, 86, 58), new(204, 168, 112), new(86, 62, 46), new(214, 218, 228)];
        return new MobKind("villager", 0.3f, 1.95f, 1.5f, 20, robes, parts) { Id = 4, OpensDoors = true };
    }

    private static MobKind MakeZombie()
    {
        Color skin = new(86, 138, 70), shirt = new(52, 150, 158), pants = new(60, 58, 138), eye = new(24, 44, 22);
        ModelPart[] parts =
        [
            new(Swing.LegA, new Vector3(-2, 12, 0), [B(-4, 0, -2, 0, 12, 2, pants)]),
            new(Swing.LegB, new Vector3(2, 12, 0), [B(0, 0, -2, 4, 12, 2, pants)]),
            new(Swing.None, Vector3.Zero, [B(-4, 12, -2, 4, 24, 2, shirt)]),
            new(Swing.ArmA, new Vector3(-6, 22, 0), [B(-8, 12, -2, -4, 24, 2, skin), B(-8.05f, 18, -2.05f, -3.95f, 24, 2.05f, shirt)]) { Pose = Ahead },
            new(Swing.ArmB, new Vector3(6, 22, 0), [B(4, 12, -2, 8, 24, 2, skin), B(3.95f, 18, -2.05f, 8.05f, 24, 2.05f, shirt)]) { Pose = Ahead },
            new(Swing.Head, new Vector3(0, 24, 0), [B(-4, 24, -4, 4, 32, 4, skin), .. Eyes(1, 28, -4, eye), B(-2, 25, -4.05f, 2, 26, -4, eye)]),
        ];
        return new MobKind("zombie", 0.3f, 1.95f, 1.0f, 20, [skin], parts)
        {
            Id = 5, Hostile = true, AttackDamage = 3, ChaseSpeed = 2.3f, FollowRange = 24, Burns = true,
        };
    }

    private static MobKind MakeSkeleton()
    {
        Color bone = new(204, 204, 198), dark = new(44, 44, 44), bow = new(112, 82, 44);
        ModelPart[] parts =
        [
            new(Swing.LegA, new Vector3(-2, 12, 0), [B(-3, 0, -1, -1, 12, 1, bone)]),
            new(Swing.LegB, new Vector3(2, 12, 0), [B(1, 0, -1, 3, 12, 1, bone)]),
            // A spine with ribs about it and shoulders across its top.
            new(Swing.None, Vector3.Zero, [B(-1, 12, -1, 1, 22, 1, bone), B(-3, 15, -1.5f, 3, 21, 1.5f, bone), B(-4, 22, -1, 4, 24, 1, bone)]),
            new(Swing.ArmA, new Vector3(-5, 22, 0), [B(-6, 12, -1, -4, 24, 1, bone)]) { Pose = Ahead },
            // The right hand holds a bow upright once the arm is held out.
            new(Swing.ArmB, new Vector3(5, 22, 0), [B(4, 12, -1, 6, 24, 1, bone), B(4.5f, 10, -4, 5.5f, 12, 4, bow)]) { Pose = Ahead },
            new(Swing.Head, new Vector3(0, 24, 0), [B(-4, 24, -4, 4, 32, 4, bone), .. Eyes(1, 27, -4, dark), B(-3, 28, -4.05f, 3, 29, -4, dark), B(-0.5f, 26, -4.05f, 0.5f, 27, -4, dark)]),
        ];
        return new MobKind("skeleton", 0.3f, 1.99f, 1.2f, 20, [bone], parts)
        {
            Id = 6, Hostile = true, ChaseSpeed = 2.4f, FollowRange = 16, Shoots = true, Burns = true,
        };
    }

    private static MobKind MakeCreeper()
    {
        Color green = new(84, 170, 64), patch = new(58, 128, 44), face = new(20, 24, 20);
        ModelPart[] parts =
        [
            new(Swing.None, Vector3.Zero, [B(-4, 6, -2, 4, 18, 2, green), B(-4.05f, 9, -1, -4, 13, 1.5f, patch), B(4, 12, -1.5f, 4.05f, 16, 1, patch)]),
            new(Swing.Head, new Vector3(0, 18, 0),
            [
                B(-4, 18, -4, 4, 26, 4, green), B(-3, 22, -4.05f, -1, 24, -4, face), B(1, 22, -4.05f, 3, 24, -4, face),
                B(-1, 19, -4.05f, 1, 22, -4, face), B(-2, 18, -4.05f, -1, 21, -4, face), B(1, 18, -4.05f, 2, 21, -4, face),
            ]),
            .. Legs(2, -4, 4, 6, 4, (x, z) => [B(x, 0, z, x + 4, 6, z + 4, patch)]),
        ];
        return new MobKind("creeper", 0.3f, 1.7f, 1.0f, 20, [green], parts)
        {
            Id = 7, Hostile = true, ChaseSpeed = 2.2f, FollowRange = 16, Explodes = true,
        };
    }

    private static MobKind MakeSpider()
    {
        Color body = new(52, 44, 40), back = new(66, 56, 50), red = new(196, 30, 30);
        var parts = new List<ModelPart>
        {
            new(Swing.None, Vector3.Zero, [B(-3, 6, -3, 3, 12, 3, body), B(-5, 5, 3, 5, 13, 15, back)]),
            new(Swing.Head, new Vector3(0, 9, -3), [B(-4, 5, -11, 4, 13, -3, body), .. Eyes(1, 10, -11, red), B(-3, 11, -11.05f, -2, 12, -11, red), B(2, 11, -11.05f, 3, 12, -11, red)]),
        };
        // Four legs a side, each reaching out along x from the thorax, tilted down and spread from
        // forward to back, the sides' sets swinging against each other.
        for (int i = 0; i < 4; i++)
        {
            float z = -2 + i * 1.3f, spread = 0.75f - i * 0.5f;
            parts.Add(new(i % 2 == 0 ? Swing.SpiderA : Swing.SpiderB, new Vector3(3, 9, z), [B(3, 8, z - 1, 18, 10, z + 1, body)]) { Pose = new Vector3(0, spread, -0.6f) });
            parts.Add(new(i % 2 == 0 ? Swing.SpiderB : Swing.SpiderA, new Vector3(-3, 9, z), [B(-18, 8, z - 1, -3, 10, z + 1, body)]) { Pose = new Vector3(0, -spread, 0.6f) });
        }
        return new MobKind("spider", 0.7f, 0.9f, 1.5f, 16, [body], [.. parts])
        {
            Id = 8, Hostile = true, AttackDamage = 2, ChaseSpeed = 3, FollowRange = 16,
        };
    }
}
