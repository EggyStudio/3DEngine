using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The player's health, food and breath in survival, as Minecraft's are: 20 points of health and of
/// food, the saturation spent before the food, and the exhaustion that spends them.
/// </summary>
/// <remarks>
/// <para>
/// Walking, sprinting, swimming, jumping, breaking blocks, hitting and healing tire the player, and
/// each four points of exhaustion take a point of saturation, or of food where none is left. With
/// 18 food or more the player heals a point every four seconds, at the cost of six exhaustion, and
/// with none left loses a point every four seconds down to one, as on Minecraft's normal difficulty.
/// Sprinting asks for more than six food.
/// </para>
/// <para>
/// A fall of more than three blocks takes a point for each block past three, a fifth of that onto
/// a hay bale and none into water. Under water the player holds fifteen seconds of breath and then
/// loses two points a second until the eye is out, and a cactus beside the player, or a block where
/// the eye is, takes a point every half second. A hit leaves half a second in which no other harms.
/// </para>
/// </remarks>
public sealed class Vitals
{
    public const int Most = 20;

    /// <summary>Seconds of breath under water.</summary>
    public const float Breath = 15;

    private const float Spell = 4, Unharmed = 0.5f;

    private float _regen, _starve, _drown, _prick;
    private Vector3 _last;
    private bool _started, _wasOnGround;

    public int Health { get; private set; } = Most;

    public int Food { get; private set; } = Most;

    public float Saturation { get; private set; } = 5;

    public float Exhaustion { get; private set; }

    /// <summary>Seconds of breath left.</summary>
    public float Air { get; private set; } = Breath;

    /// <summary>How far the player has fallen since last on the ground or in water.</summary>
    public float Fallen { get; private set; }

    /// <summary>Seconds left of the red a hit leaves on the view, in which no other hit harms.</summary>
    public float Hurt { get; private set; }

    public bool Dead => Health <= 0;

    /// <summary>What took the last of the player's health, said of the player as "you", or null while alive.</summary>
    public string? Cause { get; private set; }

    /// <summary>Whether the player has the food to sprint.</summary>
    public bool MaySprint => Food > 6;

    /// <summary>Sets the values a save kept, a new life's where none is given.</summary>
    public void Set(int health = Most, int food = Most, float saturation = 5)
    {
        (Health, Food, Saturation) = (Math.Clamp(health, 0, Most), Math.Clamp(food, 0, Most), Math.Clamp(saturation, 0, food));
        (Exhaustion, Air, Fallen, Hurt, Cause, _started) = (0, Breath, 0, 0, null, false);
        (_regen, _starve, _drown, _prick) = (0, 0, 0, 0);
    }

    /// <summary>Starts the vitals' reckoning of motion again from where the body stands, as after a teleport, so it is neither a fall nor a walk.</summary>
    public void Arrived() => (_started, Fallen) = (false, 0);

    /// <summary>
    /// Advances the vitals by a frame of so many seconds for a body that has moved: the damage of a
    /// fall as it lands, breath under water, the exhaustion of moving, and healing or starving.
    /// </summary>
    public void Update(VoxelWorld world, Body body, Vector3 eye, bool swimming, bool flying, bool sprinting, float seconds)
    {
        if (Dead) return;
        Hurt = MathF.Max(0, Hurt - seconds);
        var moved = _started ? body.Position - _last : Vector3.Zero;
        (_last, _started) = (body.Position, true);

        // Tiredness from moving along the ground or swimming, and from each jump.
        var along = new Vector2(moved.X, moved.Z).Length();
        if (!flying) Exhaust(along * (swimming ? 0.01f : sprinting ? 0.1f : 0.01f));
        if (_wasOnGround && !body.OnGround && body.Velocity.Y > 0) Exhaust(sprinting ? 0.2f : 0.05f);
        Fall(world, body, moved, flying, swimming);
        _wasOnGround = body.OnGround;

        Breathe(world, eye, seconds);
        Prick(world, body, eye, seconds);
        Hunger(seconds);
    }

    private void Fall(VoxelWorld world, Body body, Vector3 moved, bool flying, bool swimming)
    {
        var feet = world.GetBlock((int)MathF.Floor(body.Position.X), (int)MathF.Floor(body.Position.Y), (int)MathF.Floor(body.Position.Z));
        if (flying || swimming || Blocks.IsWater(feet))
        {
            Fallen = 0;
            return;
        }
        if (!body.OnGround)
        {
            if (moved.Y < 0) Fallen -= moved.Y;
            return;
        }
        if (Fallen > 3)
        {
            var under = world.GetBlock((int)MathF.Floor(body.Position.X), (int)MathF.Floor(body.Position.Y - 0.01f), (int)MathF.Floor(body.Position.Z));
            var damage = (int)MathF.Ceiling(Fallen - 3) * (under == BlockId.HayBale ? 0.2f : 1);
            if (damage >= 1) Damage((int)damage, "fell from a high place");
        }
        Fallen = 0;
    }

    private void Breathe(VoxelWorld world, Vector3 eye, float seconds)
    {
        if (!Blocks.IsWater(world.GetBlock((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z))))
        {
            // Breath comes back five times as fast as it goes.
            (Air, _drown) = (MathF.Min(Breath, Air + seconds * 5), 0);
            return;
        }
        Air = MathF.Max(0, Air - seconds);
        if (Air > 0) return;
        _drown += seconds;
        if (_drown < 1) return;
        _drown = 0;
        Damage(2, "drowned");
    }

    // A cactus beside the body's box, or a solid block where the eye is, harms every half second.
    private void Prick(VoxelWorld world, Body body, Vector3 eye, float seconds)
    {
        var cause = Blocks.IsOpaque(world.GetBlock((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z))) ? "suffocated in a wall"
            : TouchesCactus(world, body) ? "were pricked to death" : null;
        if (cause is null)
        {
            _prick = 0;
            return;
        }
        _prick += seconds;
        if (_prick < Unharmed) return;
        _prick = 0;
        Damage(1, cause);
    }

    private static bool TouchesCactus(VoxelWorld world, Body body)
    {
        Vector3 min = body.Min - new Vector3(0.05f), max = body.Max + new Vector3(0.05f);
        for (int y = (int)MathF.Floor(min.Y); y <= (int)MathF.Floor(max.Y); y++)
            for (int z = (int)MathF.Floor(min.Z); z <= (int)MathF.Floor(max.Z); z++)
                for (int x = (int)MathF.Floor(min.X); x <= (int)MathF.Floor(max.X); x++)
                    if (world.GetBlock(x, y, z) == BlockId.Cactus) return true;
        return false;
    }

    private void Hunger(float seconds)
    {
        while (Exhaustion >= 4)
        {
            Exhaustion -= 4;
            if (Saturation > 0) Saturation = MathF.Max(0, Saturation - 1);
            else Food = Math.Max(0, Food - 1);
        }
        _regen = Food >= 18 && Health < Most ? _regen + seconds : 0;
        if (_regen >= Spell)
        {
            _regen = 0;
            Health++;
            Exhaust(6);
        }
        _starve = Food == 0 ? _starve + seconds : 0;
        if (_starve >= Spell)
        {
            _starve = 0;
            if (Health > 1) Damage(1, "starved to death");
        }
    }

    /// <summary>Tires the player by an amount of exhaustion, four of which take a point of saturation or of food.</summary>
    public void Exhaust(float amount) => Exhaustion += amount;

    /// <summary>Takes some health for a cause, unless a hit less than half a second ago left the player red.</summary>
    /// <returns>Whether it harmed.</returns>
    public bool Damage(int amount, string cause)
    {
        if (Dead || Hurt > 0 || amount <= 0) return false;
        Health = Math.Max(0, Health - amount);
        Hurt = Unharmed;
        Exhaust(0.1f);
        if (Dead) Cause = cause;
        return true;
    }

    /// <summary>Eats a food, adding its points up to 20 and its saturation up to the food's points, unless the player is full or it is not food.</summary>
    public bool Eat(Item food)
    {
        if (food.Food <= 0 || Food >= Most || Dead) return false;
        Food = Math.Min(Most, Food + food.Food);
        Saturation = MathF.Min(Food, Saturation + food.Saturation);
        return true;
    }
}
