using System.Numerics;

namespace Engine;

/// <summary>
/// The directional shadow of the frame, published by <see cref="LightingUboPrepare"/>. It names the
/// light that casts it and holds, for each cascade, the light's view and projection over a slice of
/// what the window's camera sees, nearest first.
/// </summary>
/// <param name="Light">The index of the shadowed light in the frame's lighting buffer.</param>
/// <param name="Cascades">World space to each cascade's clip space, with the width in world units of one of its texels.</param>
public sealed record FrameShadow(int Light, IReadOnlyList<(Matrix4x4 ViewProjection, float Texel)> Cascades);

/// <summary>Fits a directional light's shadow cascades to what a camera sees.</summary>
/// <remarks>
/// <para>
/// The camera's view out to <see cref="Distance"/> units from its near plane is cut into
/// <see cref="Splits"/>, a cascade each, so the near one spends its texels on a few units and the
/// far one on many. All of them are tiles of one map, <see cref="AtlasSize"/> texels on a side,
/// <see cref="TileSize"/> to a tile.
/// </para>
/// <para>
/// A cascade is fitted to the sphere around its slice of the view rather than to its box, so its
/// size does not change as the camera turns, and its middle is moved in whole texels, so the edges
/// of a shadow stay still as the camera moves instead of crawling a texel at a time. It reaches
/// four times the sphere's radius further toward the light than the sphere does, so a caster above
/// the view, such as a roof over a camera indoors, still shadows it.
/// </para>
/// </remarks>
public static class ShadowFit
{
    /// <summary>A cascade's width and height in texels.</summary>
    public const int TileSize = 2048;

    /// <summary>The map the cascades are tiles of, two tiles on a side, with one left for a spot light.</summary>
    public const int AtlasSize = 2 * TileSize;

    /// <summary>How far past the camera's near plane, in world units, shadows are drawn.</summary>
    public const float Distance = 150f;

    /// <summary>Where each cascade ends, in world units past the camera's near plane.</summary>
    public static ReadOnlySpan<float> Splits => [12f, 45f, Distance];

    /// <summary>
    /// The light's view and projection for a light pointing along <paramref name="direction"/> over
    /// what <paramref name="cameraViewProjection"/> sees from <paramref name="from"/> to
    /// <paramref name="to"/> units past its near plane, or <c>false</c> when the camera's matrix
    /// cannot be inverted.
    /// </summary>
    public static bool TryFit(Matrix4x4 cameraViewProjection, Vector3 direction, float from, float to,
        out Matrix4x4 viewProjection, out float texel)
    {
        viewProjection = Matrix4x4.Identity;
        texel = 0;
        if (!Matrix4x4.Invert(cameraViewProjection, out var inverse) || direction.LengthSquared() < 1e-8f) return false;

        // The slice of the camera's view, along each edge of the frustum from its near corner from
        // the one distance to the other, cut short where the far plane comes first.
        Span<Vector3> corners = stackalloc Vector3[8];
        int n = 0;
        for (int y = -1; y <= 1; y += 2)
            for (int x = -1; x <= 1; x += 2)
            {
                var near = Unproject(inverse, x, y, 0);
                var far = Unproject(inverse, x, y, 1);
                var length = Vector3.Distance(near, far);
                var way = length > 0 ? (far - near) / length : Vector3.Zero;
                corners[n++] = near + way * MathF.Min(from, length);
                corners[n++] = near + way * MathF.Min(to, length);
            }

        var center = Vector3.Zero;
        foreach (var corner in corners) center += corner;
        center /= corners.Length;
        float radius = 0;
        foreach (var corner in corners) radius = MathF.Max(radius, Vector3.Distance(center, corner));
        if (!float.IsFinite(radius) || radius <= 0) return false;

        // Rounded up to a sixteenth of a unit, so rounding error in the corners as the camera
        // moves does not change the texel's size, which would move every texel's edge.
        radius = MathF.Ceiling(radius * 16) / 16;

        direction = Vector3.Normalize(direction);
        var up = MathF.Abs(direction.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, direction, up);

        // The middle of the cascade in the light's view, moved to a whole texel.
        texel = 2 * radius / TileSize;
        var middle = Vector3.Transform(center, view);
        middle.X = MathF.Floor(middle.X / texel) * texel;
        middle.Y = MathF.Floor(middle.Y / texel) * texel;

        // The view looks down -Z, so the near plane is the larger distance along it.
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            middle.X - radius, middle.X + radius, middle.Y - radius, middle.Y + radius,
            -(middle.Z + radius) - 4 * radius, -(middle.Z - radius));
        viewProjection = view * projection;
        return true;
    }

    /// <summary>Every cascade of a light pointing along <paramref name="direction"/> over what the camera sees, nearest first, or none when it cannot be fitted.</summary>
    public static (Matrix4x4 ViewProjection, float Texel)[] FitCascades(Matrix4x4 cameraViewProjection, Vector3 direction)
    {
        var splits = Splits;
        var cascades = new (Matrix4x4, float)[splits.Length];
        float from = 0;
        for (int i = 0; i < splits.Length; i++)
        {
            if (!TryFit(cameraViewProjection, direction, from, splits[i], out var viewProjection, out var texel)) return [];
            cascades[i] = (viewProjection, texel);
            from = splits[i];
        }
        return cascades;
    }

    /// <summary>The texel at which cascade or tile <paramref name="tile"/> starts in the map, across and down.</summary>
    public static (int X, int Y) TileOrigin(int tile) => (tile % 2 * TileSize, tile / 2 * TileSize);

    private static Vector3 Unproject(in Matrix4x4 inverse, float x, float y, float z)
    {
        var v = Vector4.Transform(new Vector4(x, y, z, 1), inverse);
        return new Vector3(v.X, v.Y, v.Z) / v.W;
    }
}

/// <summary>
/// Render graph node that draws the window's meshes into the shadow map from the frame's shadowed
/// light, before any pass reads it.
/// </summary>
public sealed class ShadowNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<FrameShadow>() is not { } shadow) return;
        renderWorld.TryGet<ModelRenderer>()?.DrawShadow(renderContext, renderWorld, shadow);
    }
}
