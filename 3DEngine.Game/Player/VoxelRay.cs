using System.Numerics;

namespace Engine.Game;

/// <summary>The block a ray met, the face it entered through as that face's outward normal, and how far along the ray.</summary>
public readonly record struct BlockHit(int X, int Y, int Z, int NormalX, int NormalY, int NormalZ, float Distance)
{
    /// <summary>The block beside the face that was hit, where a block placed on it goes.</summary>
    public (int X, int Y, int Z) Beside => (X + NormalX, Y + NormalY, Z + NormalZ);

    /// <summary>Whether the ray began inside the block, so no face was crossed.</summary>
    public bool Inside => NormalX == 0 && NormalY == 0 && NormalZ == 0;
}

/// <summary>Steps a ray through the grid of blocks one block at a time, after Amanatides and Woo.</summary>
public static class VoxelRay
{
    /// <summary>The first block the crosshair rests on along a ray within <paramref name="reach"/>, passing through air and water, or null.</summary>
    public static BlockHit? Cast(VoxelWorld world, Vector3 origin, Vector3 direction, float reach)
    {
        direction = Vector3.Normalize(direction);
        int x = (int)MathF.Floor(origin.X), y = (int)MathF.Floor(origin.Y), z = (int)MathF.Floor(origin.Z);
        int stepX = Math.Sign(direction.X), stepY = Math.Sign(direction.Y), stepZ = Math.Sign(direction.Z);
        float deltaX = Across(direction.X), deltaY = Across(direction.Y), deltaZ = Across(direction.Z);
        float nextX = First(origin.X, x, direction.X, deltaX), nextY = First(origin.Y, y, direction.Y, deltaY), nextZ = First(origin.Z, z, direction.Z, deltaZ);
        int nx = 0, ny = 0, nz = 0;
        float travelled = 0;

        while (travelled <= reach)
        {
            var block = world.GetBlock(x, y, z);
            if (Blocks.IsTarget(block))
            {
                if (Blocks.Get(block).Shape is not { } shape) return new BlockHit(x, y, z, nx, ny, nz, travelled);
                // A block smaller than its cell is met only where the ray crosses the box around its pieces.
                var cell = new Vector3(x, y, z);
                if (Enters(origin, direction, cell + shape.Min, cell + shape.Max, out var at, out var normal) && at <= reach)
                    return new BlockHit(x, y, z, normal.X, normal.Y, normal.Z, at);
            }
            if (nextX < nextY && nextX < nextZ)
            {
                x += stepX;
                travelled = nextX;
                nextX += deltaX;
                (nx, ny, nz) = (-stepX, 0, 0);
            }
            else if (nextY < nextZ)
            {
                y += stepY;
                travelled = nextY;
                nextY += deltaY;
                (nx, ny, nz) = (0, -stepY, 0);
            }
            else
            {
                z += stepZ;
                travelled = nextZ;
                nextZ += deltaZ;
                (nx, ny, nz) = (0, 0, -stepZ);
            }
        }
        return null;
    }

    // Where a ray enters a box and the outward normal of the face it enters through, by the slabs
    // between the box's faces on each axis. A ray that starts inside enters at once through no face.
    private static bool Enters(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, out float at, out (int X, int Y, int Z) normal)
    {
        float near = 0, far = float.PositiveInfinity;
        (at, normal) = (0, (0, 0, 0));
        for (int axis = 0; axis < 3; axis++)
        {
            float o = origin[axis], d = direction[axis], lo = min[axis], hi = max[axis];
            if (d == 0)
            {
                if (o < lo || o > hi) return false;
                continue;
            }
            float t0 = (lo - o) / d, t1 = (hi - o) / d;
            var sign = -1;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
                sign = 1;
            }
            if (t0 > near)
            {
                near = t0;
                normal = axis == 0 ? (sign, 0, 0) : axis == 1 ? (0, sign, 0) : (0, 0, sign);
            }
            far = MathF.Min(far, t1);
            if (near > far) return false;
        }
        at = near;
        return true;
    }

    // How far along the ray it takes to cross one block on an axis.
    private static float Across(float direction) => direction == 0 ? float.PositiveInfinity : MathF.Abs(1 / direction);

    // How far along the ray the first boundary on an axis lies.
    private static float First(float origin, int cell, float direction, float across) =>
        direction > 0 ? (cell + 1 - origin) * across : direction < 0 ? (origin - cell) * across : float.PositiveInfinity;
}
