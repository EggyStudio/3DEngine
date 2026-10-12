using System.Numerics;

namespace Engine.Game;

/// <summary>The player as a hostile mob hunts it: the box and the eye.</summary>
public readonly record struct MobTarget(Body Body, Vector3 Eye);

/// <summary>Something a mob does to the player, which the game carries out.</summary>
public abstract record MobEvent(Mob Mob);

/// <summary>A strike of so much damage from where the mob stands.</summary>
public sealed record MobStrike(Mob Mob, int Damage, Vector3 From) : MobEvent(Mob);

/// <summary>An arrow loosed from a place with a velocity.</summary>
public sealed record MobShot(Mob Mob, Vector3 From, Vector3 Velocity) : MobEvent(Mob);

/// <summary>An explosion of a power at a place, as a creeper's of three.</summary>
public sealed record MobBlast(Mob Mob, Vector3 At, float Power) : MobEvent(Mob);

public sealed partial class Mob
{
    // A strike or a shot every second or two, and a creeper's fuse of a second and a half, which it
    // gives up where the player gets seven blocks away, as Minecraft's do.
    private const float StrikeEvery = 1, ShotEvery = 2, FuseTime = 1.5f, FuseReach = 3, FuseLeave = 7, ArrowSpeed = 30;

    private float _repath, _strikeWait, _shotWait = 1, _burn;
    private bool _angry, _hunting;

    /// <summary>Seconds a creeper's fuse has burned, or less than zero while it is not lit.</summary>
    public float Fuse { get; private set; } = -1;

    /// <summary>Whether it blew itself up, which leaves nothing to drop.</summary>
    public bool Exploded { get; private set; }

    // The target where it is the hunter's to hunt: within its range and seen, or anywhere within
    // twice that once the hunt has begun or a hit has angered it.
    private MobTarget? Hunts(VoxelWorld world, MobTarget? target)
    {
        if (target is not { } prey)
        {
            _hunting = false;
            return null;
        }
        var eye = Body.Position + new Vector3(0, Kind.Eyes, 0);
        var distance = Vector3.Distance(eye, prey.Eye);
        var range = _hunting || _angry ? Kind.FollowRange * 2 : Kind.FollowRange;
        _hunting = distance <= range && (_hunting || _angry || Sees(world, eye, prey.Eye));
        if (!_hunting) Fuse = -1;
        return _hunting ? prey : null;
    }

    /// <summary>Whether no block stands between two points.</summary>
    public static bool Sees(VoxelWorld world, Vector3 from, Vector3 to)
    {
        var way = to - from;
        var length = way.Length();
        return length < 1e-3f || VoxelRay.Cast(world, from, way / length, length) is not { } hit || hit.Distance >= length - 0.05f;
    }

    private (Vector3 Wish, bool Jump) Hunt(VoxelWorld world, MobTarget prey, float seconds, List<MobEvent> events)
    {
        _strikeWait -= seconds;
        _shotWait -= seconds;
        _repath -= seconds;
        var eye = Body.Position + new Vector3(0, Kind.Eyes, 0);
        var to = prey.Body.Position - Body.Position;
        var flat = new Vector2(to.X, to.Z);
        var distance = to.Length();
        Face(prey.Eye, seconds);

        if (Kind.Explodes)
        {
            if (Fuse >= 0)
            {
                Fuse = distance > FuseLeave ? -1 : Fuse + seconds;
                if (Fuse < FuseTime) return (Vector3.Zero, false);
                events.Add(new MobBlast(this, Body.Position + new Vector3(0, Kind.Height / 2, 0), 3));
                (Exploded, Health, Dying) = (true, 0, DeathTime);
                return (Vector3.Zero, false);
            }
            if (distance < FuseReach && Sees(world, eye, prey.Eye))
            {
                Fuse = 0;
                return (Vector3.Zero, false);
            }
        }

        if (Kind.Shoots && distance < 15 && Sees(world, eye, prey.Eye))
        {
            if (_shotWait <= 0)
            {
                _shotWait = ShotEvery;
                events.Add(new MobShot(this, eye + Vector3.Normalize(prey.Eye - eye) * 0.6f, Aim(eye, prey)));
            }
            // It keeps between five and ten blocks off, backing away from a player who closes in.
            if (distance < 5 && flat.LengthSquared() > 1e-4f)
            {
                _path = null;
                var away = -Vector2.Normalize(flat) * Kind.ChaseSpeed * 0.7f;
                return (new Vector3(away.X, 0, away.Y), false);
            }
            if (distance < 10)
            {
                _path = null;
                return (Vector3.Zero, false);
            }
        }

        // Within reach of the player, a strike each second, still pressing in.
        if (!Kind.Shoots && !Kind.Explodes && flat.Length() < Kind.HalfWidth + prey.Body.HalfWidth + 0.7f && MathF.Abs(to.Y) < 1.5f)
        {
            if (_strikeWait <= 0)
            {
                _strikeWait = StrikeEvery;
                events.Add(new MobStrike(this, Kind.AttackDamage, Body.Position));
            }
            return (Ahead(flat) * Kind.ChaseSpeed * 0.3f, false);
        }

        // Near and on its level it runs straight at the player, and otherwise along a way found to
        // where the player stands, found again every half second.
        if (flat.Length() < 3 && MathF.Abs(to.Y) < 0.6f)
        {
            _path = null;
            return (Ahead(flat) * Kind.ChaseSpeed, Body.OnGround && Blocked(world, flat));
        }
        if (_repath <= 0 || _path is null)
        {
            _repath = 0.5f;
            if (Ground(world, prey.Body.Position) is not { } goal || !WalkTo(world, goal)) _path = null;
        }
        if (_path is null) return (Ahead(flat) * Kind.ChaseSpeed, Body.OnGround && Blocked(world, flat));
        return Follow(world, seconds, Kind.ChaseSpeed);
    }

    private static Vector3 Ahead(Vector2 flat) => flat.LengthSquared() > 1e-6f ? new Vector3(Vector2.Normalize(flat).X, 0, Vector2.Normalize(flat).Y) : Vector3.Zero;

    // Whether a block stands a step ahead at the feet with room above it, which a jump climbs.
    private bool Blocked(VoxelWorld world, Vector2 flat)
    {
        if (flat.LengthSquared() < 1e-6f) return false;
        var ahead = Body.Position + new Vector3(Vector2.Normalize(flat).X, 0, Vector2.Normalize(flat).Y) * (Kind.HalfWidth + 0.4f);
        int x = (int)MathF.Floor(ahead.X), y = (int)MathF.Floor(Body.Position.Y + 0.1f), z = (int)MathF.Floor(ahead.Z);
        return world.BoxesAt(x, y, z).Count > 0 && world.BoxesAt(x, y + 1, z).Count == 0 && world.BoxesAt(x, y + 2, z).Count == 0;
    }

    // The cell the mob can stand in at or under a place, up to four blocks down, as under a player in the air.
    private Step? Ground(VoxelWorld world, Vector3 at)
    {
        int x = (int)MathF.Floor(at.X), y = (int)MathF.Floor(at.Y + 0.2f), z = (int)MathF.Floor(at.Z);
        for (int down = 0; down <= 4; down++)
            if (Pathfinder.Stands(world, Kind, new Step(x, y - down, z))) return new Step(x, y - down, z);
        return null;
    }

    // An arrow's velocity at the player's chest from the bow, raised for the drop of its flight under
    // the arrow's gravity of 20, with a little scatter, as a skeleton's on normal difficulty.
    private Vector3 Aim(Vector3 from, MobTarget prey)
    {
        var chest = prey.Body.Position + new Vector3(0, prey.Body.Height * 0.6f, 0);
        var to = chest - from;
        var flat = new Vector2(to.X, to.Z).Length();
        var time = flat / ArrowSpeed;
        to.Y += 0.5f * Arrows.Gravity * time * time;
        var scatter = new Vector3((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f) * 0.06f;
        return Vector3.Normalize(Vector3.Normalize(to) + scatter) * ArrowSpeed;
    }

    // Turns the head to look at a point and the body after it.
    private void Face(Vector3 point, float seconds)
    {
        var eye = Body.Position + new Vector3(0, Kind.Eyes, 0);
        var to = point - eye;
        var yaw = MathF.Atan2(-to.X, -to.Z);
        HeadYaw = Turn(HeadYaw, yaw, 10 * seconds);
        Yaw = Turn(Yaw, yaw, 6 * seconds);
        var flat = MathF.Sqrt(to.X * to.X + to.Z * to.Z);
        HeadPitch += (Math.Clamp(MathF.Atan2(to.Y, flat), -0.8f, 0.8f) - HeadPitch) * MathF.Min(1, 8 * seconds);
    }

    // Under the open sky by day, out of water, a point of health a second, as Minecraft's undead burn.
    private void Burn(VoxelWorld world, float hour, float seconds)
    {
        var head = Body.Position + new Vector3(0, Kind.Height - 0.1f, 0);
        int x = (int)MathF.Floor(head.X), y = (int)MathF.Floor(head.Y), z = (int)MathF.Floor(head.Z);
        if (hour < 6.5f || hour > 17.5f || _swimming || world.GetLight(x, y, z).Sky < Lighting.Max)
        {
            _burn = 0;
            return;
        }
        _burn += seconds;
        if (_burn < 1) return;
        _burn = 0;
        Harm(1);
    }
}
