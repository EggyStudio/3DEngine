using System.Numerics;

namespace Engine;

/// <summary>
/// The shadows of the frame, published by <see cref="LightingUboPrepare"/>. It names the directional
/// light that casts them and holds, for each cascade, the light's view and projection over a slice
/// of what the window's camera sees, nearest first, and names the spot light drawn into the map's
/// last tile with its view and projection.
/// </summary>
/// <param name="Light">The index of the shadowed directional light in the frame's lighting buffer, or -1 for none.</param>
/// <param name="Cascades">World space to each cascade's clip space, with the width in world units of one of its texels.</param>
/// <param name="SpotLights">
/// The shadowed spot lights, by their index in the lighting buffer, each with world space to its
/// clip space and the width of one of its texels per unit of distance from it.
/// </param>
/// <param name="PointLights">
/// The shadowed point lights, by their index in the lighting buffer, each with the view and
/// projection of the six faces around it, in the order +X, -X, +Y, -Y, +Z, -Z.
/// </param>
/// <param name="TileSize">The width in texels of a cascade's tile, half the map's.</param>
/// <param name="PointFaceSize">The width in texels of each face of a point light.</param>
internal sealed record FrameShadow(int Light, IReadOnlyList<(Matrix4x4 ViewProjection, float Texel)> Cascades,
    IReadOnlyList<(int Light, Matrix4x4 ViewProjection, float TexelPerUnit)>? SpotLights = null,
    IReadOnlyList<(int Light, Matrix4x4[] Faces)>? PointLights = null,
    int TileSize = ShadowFit.TileSize, int PointFaceSize = ShadowFit.PointFaceSize);

/// <summary>How far shadows reach, a world resource the renderer reads each frame.</summary>
internal sealed class ShadowSettings
{
    /// <summary>
    /// How far past the camera's near plane, in world units, a directional light's shadows are
    /// drawn, and how far a spot or point light with no range casts them. The cascades split it in
    /// the default's proportions, so a shorter distance gives sharper shadows over less ground.
    /// </summary>
    public float Distance { get; set; } = ShadowFit.Distance;

    /// <summary>
    /// The width in texels of each tile of the map, a cascade's or the spot lights', the map being
    /// two tiles on a side, 2048 unless set. A point light's faces are a quarter of it.
    /// </summary>
    public int TileSize { get; set; } = ShadowFit.TileSize;
}

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
internal static class ShadowFit
{
    /// <summary>A cascade's width and height in texels, unless <see cref="ShadowSettings.TileSize"/> says otherwise.</summary>
    public const int TileSize = 2048;

    /// <summary>The map the cascades are tiles of, two tiles on a side, with one left for a spot light, at the default tile size.</summary>
    public const int AtlasSize = 2 * TileSize;

    /// <summary>How far past the camera's near plane, in world units, shadows are drawn unless <see cref="ShadowSettings"/> says otherwise.</summary>
    public const float Distance = 150f;

    /// <summary>Where each cascade ends, in world units past the camera's near plane, at the default distance.</summary>
    public static ReadOnlySpan<float> Splits => [12f, 45f, Distance];

    /// <summary>Where each cascade ends for shadows drawn out to <paramref name="distance"/>, in the default's proportions.</summary>
    public static float[] SplitsFor(float distance) => [distance * 12f / Distance, distance * 45f / Distance, distance];

    /// <summary>
    /// The light's view and projection for a light pointing along <paramref name="direction"/> over
    /// what <paramref name="cameraViewProjection"/> sees from <paramref name="from"/> to
    /// <paramref name="to"/> units past its near plane, or <c>false</c> when the camera's matrix
    /// cannot be inverted.
    /// </summary>
    public static bool TryFit(Matrix4x4 cameraViewProjection, Vector3 direction, float from, float to,
        out Matrix4x4 viewProjection, out float texel, int tileSize = TileSize) =>
        TryFit([cameraViewProjection], direction, from, to, out viewProjection, out texel, tileSize);

    /// <summary>
    /// The light's view and projection for a light pointing along <paramref name="direction"/> over
    /// what every one of <paramref name="cameras"/> sees from <paramref name="from"/> to
    /// <paramref name="to"/> units past its near plane, one cascade for views that share it, or
    /// <c>false</c> when a camera's matrix cannot be inverted.
    /// </summary>
    public static bool TryFit(ReadOnlySpan<Matrix4x4> cameras, Vector3 direction, float from, float to,
        out Matrix4x4 viewProjection, out float texel, int tileSize = TileSize)
    {
        viewProjection = Matrix4x4.Identity;
        texel = 0;
        if (cameras.IsEmpty || direction.LengthSquared() < 1e-8f) return false;

        // The slice of each camera's view, along each edge of the frustum from its near corner from
        // the one distance to the other, cut short where the far plane comes first.
        Span<Vector3> corners = stackalloc Vector3[8 * cameras.Length];
        int n = 0;
        foreach (var camera in cameras)
        {
            if (!Matrix4x4.Invert(camera, out var inverse)) return false;
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
        texel = 2 * radius / tileSize;
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

    /// <summary>
    /// Every cascade of a light pointing along <paramref name="direction"/> over what the camera
    /// sees out to <paramref name="distance"/>, nearest first, or none when it cannot be fitted.
    /// </summary>
    public static (Matrix4x4 ViewProjection, float Texel)[] FitCascades(Matrix4x4 cameraViewProjection, Vector3 direction, float distance = Distance,
        int tileSize = TileSize) =>
        FitCascades([cameraViewProjection], direction, distance, tileSize);

    /// <summary>The cascades fitted to every one of <paramref name="cameras"/> at once, for views that share them.</summary>
    public static (Matrix4x4 ViewProjection, float Texel)[] FitCascades(ReadOnlySpan<Matrix4x4> cameras, Vector3 direction, float distance = Distance,
        int tileSize = TileSize)
    {
        var splits = SplitsFor(distance);
        var cascades = new (Matrix4x4, float)[splits.Length];
        float from = 0;
        for (int i = 0; i < splits.Length; i++)
        {
            if (!TryFit(cameras, direction, from, splits[i], out var viewProjection, out var texel, tileSize)) return [];
            cascades[i] = (viewProjection, texel);
            from = splits[i];
        }
        return cascades;
    }

    /// <summary>The tile spot lights' shadows are drawn into, after the cascades.</summary>
    public const int SpotTile = 3;

    /// <summary>How many spot lights cast shadows at once, those with <c>CastsShadows</c> set that matter most to the view.</summary>
    public const int MaxSpotLights = 10;

    /// <summary>
    /// The square of the spot tile a spot light's shadow is drawn into, by its slot among
    /// <paramref name="count"/> shadowed spot lights, the slots ranked by how much the light matters
    /// to the view: the whole tile for one, a quarter each for up to four, and past four a quarter
    /// each for the first two and a sixteenth each for the rest, in the tile's lower half.
    /// </summary>
    public static (int X, int Y, int Size) SpotTileArea(int slot, int count, int tileSize = TileSize)
    {
        var (x, y) = TileOrigin(SpotTile, tileSize);
        if (count <= 1) return (x, y, tileSize);
        var half = tileSize / 2;
        if (count <= 4 || slot < 2) return (x + slot % 2 * half, y + slot / 2 * half, half);
        var quarter = tileSize / 4;
        var small = slot - 2;
        return (x + small % 4 * quarter, y + half + small / 4 * quarter, quarter);
    }

    /// <summary>
    /// The view and projection of a spot light at <paramref name="position"/> pointing along
    /// <paramref name="direction"/>, wide enough for its outer cone and reaching
    /// <paramref name="range"/>, or <see cref="Distance"/> for a light with no range, and the width
    /// of one texel per unit of distance from the light.
    /// </summary>
    public static bool TryFitSpot(Vector3 position, Vector3 direction, float cosOuter, float range,
        out Matrix4x4 viewProjection, out float texelPerUnit, float distance = Distance, int tileSize = TileSize)
    {
        viewProjection = Matrix4x4.Identity;
        texelPerUnit = 0;
        if (direction.LengthSquared() < 1e-8f) return false;
        direction = Vector3.Normalize(direction);

        // A little past the outer cone, so the cone's edge falls inside the tile.
        var half = MathF.Min(MathF.Acos(Math.Clamp(cosOuter, -1f, 1f)) * 1.05f, MathF.PI * 0.45f);
        var far = range > 0 ? range : distance;
        var up = MathF.Abs(direction.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        viewProjection = Matrix4x4.CreateLookAt(position, position + direction, up)
                         * Matrix4x4.CreatePerspectiveFieldOfView(2 * half, 1, MathF.Max(0.05f, far / 2000), far);
        texelPerUnit = 2 * MathF.Tan(half) / tileSize;
        return true;
    }

    /// <summary>How many point lights cast shadows at once, those with <c>CastsShadows</c> set that matter most to the view.</summary>
    public const int MaxPointLights = 12;

    /// <summary>How many of them, the ones that matter most, draw each face into a layer of its own at <see cref="PointFaceSize"/>.</summary>
    public const int FullPointLights = 4;

    /// <summary>The layers of the point lights' map: six for each full light, then the rest's faces at half the size, four to a layer.</summary>
    public const int PointLayers = FullPointLights * 6 + (MaxPointLights - FullPointLights) * 6 / 4;

    /// <summary>
    /// Where a point light's face is drawn in the point map, by its slot: a layer of its own for the
    /// first <see cref="FullPointLights"/>, and for the rest a quarter of a layer after theirs, at
    /// half the size, so a level of a dozen lamps shadows each.
    /// </summary>
    public static (int Layer, int X, int Y, int Size) PointFaceArea(int slot, int face, int faceSize = PointFaceSize)
    {
        if (slot < FullPointLights) return (slot * 6 + face, 0, 0, faceSize);
        var index = (slot - FullPointLights) * 6 + face;
        var half = faceSize / 2;
        return (FullPointLights * 6 + index / 4, index % 4 % 2 * half, index % 4 / 2 * half, half);
    }

    /// <summary>The width and height in texels of each of a point light's six faces, a quarter of the default tile.</summary>
    public const int PointFaceSize = TileSize / 4;

    /// <summary>
    /// How much wider than a right angle each face of a point light is, so the nine depths compared
    /// around a point near a face's edge stay inside it.
    /// </summary>
    public const float PointFaceSlack = 1.02f;

    /// <summary>
    /// The views and projections of the six faces around a point light at
    /// <paramref name="position"/>, reaching <paramref name="range"/>, or <see cref="Distance"/>
    /// for a light with no range, in the order +X, -X, +Y, -Y, +Z, -Z, which the shader picks
    /// between by the axis a point lies furthest along.
    /// </summary>
    public static Matrix4x4[] FitPoint(Vector3 position, float range, float distance = Distance)
    {
        var far = range > 0 ? range : distance;
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(2 * MathF.Atan(PointFaceSlack), 1, MathF.Max(0.05f, far / 2000), far);
        Vector3[] axes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        var faces = new Matrix4x4[6];
        for (int f = 0; f < 6; f++)
        {
            var up = MathF.Abs(axes[f].Y) > 0.5f ? Vector3.UnitZ : Vector3.UnitY;
            faces[f] = Matrix4x4.CreateLookAt(position, position + axes[f], up) * projection;
        }
        return faces;
    }

    /// <summary>The texel at which cascade or tile <paramref name="tile"/> starts in the map, across and down.</summary>
    public static (int X, int Y) TileOrigin(int tile, int tileSize = TileSize) => (tile % 2 * tileSize, tile / 2 * tileSize);

    private static Vector3 Unproject(in Matrix4x4 inverse, float x, float y, float z)
    {
        var v = Vector4.Transform(new Vector4(x, y, z, 1), inverse);
        return new Vector3(v.X, v.Y, v.Z) / v.W;
    }
}

/// <summary>
/// Render graph node that captures a reflection probe whose capture is out of date, after the
/// window's shadow map is drawn, so the probe sees the room lit as the window does.
/// </summary>
internal sealed class ProbeNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<ModelRenderer>()?.CaptureProbes(renderContext, renderWorld);
}

/// <summary>
/// Render graph node that draws the window's meshes into the shadow map from the frame's shadowed
/// lights, after the render targets, which draw it for their own cameras, and before the window's
/// passes read it.
/// </summary>
internal sealed class ShadowNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<FrameShadow>() is not { } shadow) return;
        renderWorld.TryGet<ModelRenderer>()?.DrawShadow(renderContext, renderWorld, shadow);
    }
}
