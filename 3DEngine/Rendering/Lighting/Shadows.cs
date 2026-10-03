using System.Numerics;

namespace Engine;

/// <summary>
/// The directional shadow of the frame, published by <see cref="LightingUboPrepare"/>. It names the
/// light that casts it and holds the light's view and projection over what the window's camera sees.
/// </summary>
/// <param name="Light">The index of the shadowed light in the frame's lighting buffer.</param>
/// <param name="ViewProjection">World space to the shadow map's clip space.</param>
/// <param name="Texel">The width in world units of one texel of the map.</param>
public sealed record FrameShadow(int Light, Matrix4x4 ViewProjection, float Texel);

/// <summary>Fits a directional light's shadow map to what a camera sees.</summary>
/// <remarks>
/// <para>
/// One map covers the camera's view out to <see cref="Distance"/> units from its near plane. The
/// map is fitted to the sphere around that part of the view rather than to its box, so its size
/// does not change as the camera turns, and its middle is moved in whole texels, so the edges of a
/// shadow stay still as the camera moves instead of crawling a texel at a time.
/// </para>
/// <para>
/// The map reaches four times the sphere's radius further toward the light than the sphere does,
/// so a caster above the view, such as a roof over a camera indoors, still shadows it.
/// </para>
/// </remarks>
public static class ShadowFit
{
    /// <summary>The shadow map's width and height in texels.</summary>
    public const int MapSize = 2048;

    /// <summary>How far past the camera's near plane, in world units, shadows are drawn.</summary>
    public const float Distance = 40f;

    /// <summary>
    /// The light's view and projection for a light pointing along <paramref name="direction"/>
    /// over what <paramref name="cameraViewProjection"/> sees, or <c>false</c> when the camera's
    /// matrix cannot be inverted.
    /// </summary>
    public static bool TryFit(Matrix4x4 cameraViewProjection, Vector3 direction, out Matrix4x4 viewProjection, out float texel)
    {
        viewProjection = Matrix4x4.Identity;
        texel = 0;
        if (!Matrix4x4.Invert(cameraViewProjection, out var inverse) || direction.LengthSquared() < 1e-8f) return false;

        // The camera's view out to the distance: each edge of the frustum from its near corner,
        // cut short where the far plane lies further than the distance.
        Span<Vector3> corners = stackalloc Vector3[8];
        int n = 0;
        for (int y = -1; y <= 1; y += 2)
            for (int x = -1; x <= 1; x += 2)
            {
                var near = Unproject(inverse, x, y, 0);
                var far = Unproject(inverse, x, y, 1);
                var length = Vector3.Distance(near, far);
                corners[n++] = near;
                corners[n++] = length > Distance ? near + (far - near) * (Distance / length) : far;
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

        // The middle of the map in the light's view, moved to a whole texel.
        texel = 2 * radius / MapSize;
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
