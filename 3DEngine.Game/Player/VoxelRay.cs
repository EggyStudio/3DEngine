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
            if (Blocks.IsTarget(world.GetBlock(x, y, z))) return new BlockHit(x, y, z, nx, ny, nz, travelled);
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

    // How far along the ray it takes to cross one block on an axis.
    private static float Across(float direction) => direction == 0 ? float.PositiveInfinity : MathF.Abs(1 / direction);

    // How far along the ray the first boundary on an axis lies.
    private static float First(float origin, int cell, float direction, float across) =>
        direction > 0 ? (cell + 1 - origin) * across : direction < 0 ? (origin - cell) * across : float.PositiveInfinity;
}
