using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- The texture coordinates of an edit mesh's selected faces, projected, moved, scaled, turned,
    // set to a texel density and put back to the default, each a line of the recipe, so a saved
    // model's coordinates are made again with its shape.

    /// <summary>A face's texture coordinates, one for each of its corners in their order, its own or the world-aligned default.</summary>
    /// <remarks>
    /// The default is each corner's place in meters seen along the axis the face's normal is
    /// nearest, which follows the face wherever it moves. A face given its own by one of the
    /// <c>EditMeshUv</c> calls keeps them on its corners as they move, and a cut or an inset gives the
    /// corners it makes the coordinates the face had there.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The mesh has no such face.</exception>
    public static Vector2[] GetEditMeshFaceUvs(EditMesh mesh, int face) =>
        Edited(mesh, nameof(GetEditMeshFaceUvs)) is { } data ? data.UvsOf(face) : [];

    /// <summary>Gives the selected faces texture coordinates by a planar projection along a direction, in meters.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="facing">The way the projection looks back along, from the faces toward its plane, as up for a floor, or the way back toward a camera for its view.</param>
    /// <remarks>
    /// In meters, as the default is, the picture's first coordinate running to the right seen from
    /// that side and its second down, the world's up being up in the picture where the direction is
    /// not straight up or down, so a projection along an axis gives a face facing that axis its
    /// default coordinates. The recipe's line is <c>uv planar +z</c>, or three numbers.
    /// </remarks>
    /// <exception cref="ArgumentException">The direction is zero or not finite.</exception>
    public static void EditMeshUvPlanar(EditMesh mesh, Vector3 facing) => Edited(mesh, nameof(EditMeshUvPlanar))?.UvPlanar(facing);

    /// <summary>Gives the selected faces the world-aligned default as texture coordinates of their own, which then follow their corners.</summary>
    /// <remarks>
    /// Each face along the axis its normal is nearest, written onto the faces so a move after it
    /// carries the coordinates with the face, and the start for a move, a scale or a turn of a face
    /// that has none of its own. The recipe's line is <c>uv box</c>.
    /// </remarks>
    public static void EditMeshUvBox(EditMesh mesh) => Edited(mesh, nameof(EditMeshUvBox))?.UvBox();

    /// <summary>Gives the selected faces texture coordinates by a cylindrical projection about an axis.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="axis">The axis, any length but zero.</param>
    /// <param name="center">A point the axis passes through, or null for the middle of the box holding the selection.</param>
    /// <remarks>
    /// The first coordinate is the distance round the axis at the selection's mean distance from it,
    /// and the second the distance down along it, both in meters, so a cylinder's side shows the
    /// grid's meter as its flat faces do. A face across the seam is wrapped from its own middle, so it
    /// is never stretched round the whole way. The recipe's line is <c>uv cylinder y</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">The axis is zero or not finite.</exception>
    public static void EditMeshUvCylinder(EditMesh mesh, Vector3 axis, Vector3? center = null) =>
        Edited(mesh, nameof(EditMeshUvCylinder))?.UvCylinder(axis, center);

    /// <summary>Gives the selected faces texture coordinates by a spherical projection about a point.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="center">The sphere's middle, or null for the middle of the box holding the selection.</param>
    /// <remarks>
    /// The first coordinate is the distance round the world's up axis and the second the distance
    /// down from the equator, along the sphere of the selection's mean distance from its middle, in
    /// meters. The recipe's line is <c>uv sphere</c>.
    /// </remarks>
    public static void EditMeshUvSphere(EditMesh mesh, Vector3? center = null) => Edited(mesh, nameof(EditMeshUvSphere))?.UvSphere(center);

    /// <summary>Moves the selected faces' texture coordinates, a whole number leaving a repeating texture where it was.</summary>
    /// <remarks>The recipe's line is <c>uv move 0.5 0</c>.</remarks>
    /// <exception cref="ArgumentException">A part is not finite.</exception>
    public static void EditMeshUvMove(EditMesh mesh, Vector2 offset) => Edited(mesh, nameof(EditMeshUvMove))?.UvMove(offset.X, offset.Y);

    /// <summary>Scales the selected faces' texture coordinates about their middle, two showing the picture twice as often, as a whole or each face about its own middle.</summary>
    /// <remarks>The recipe's line is <c>uv scale 2</c>, or <c>uv scale 2 1 each</c>.</remarks>
    /// <exception cref="ArgumentException">A factor is zero or not finite.</exception>
    public static void EditMeshUvScale(EditMesh mesh, Vector2 factor, bool each = false) =>
        Edited(mesh, nameof(EditMeshUvScale))?.UvScale(factor.X, factor.Y, each);

    /// <summary>Turns the selected faces' texture coordinates about their middle, from the first coordinate toward the second, as a whole or each face about its own middle.</summary>
    /// <remarks>The recipe's line is <c>uv rotate 90</c>, or <c>uv rotate 90 each</c>.</remarks>
    /// <exception cref="ArgumentException">The angle is not finite.</exception>
    public static void EditMeshUvRotate(EditMesh mesh, float degrees, bool each = false) =>
        Edited(mesh, nameof(EditMeshUvRotate))?.UvRotate(degrees, each);

    /// <summary>Scales the selected faces' texture coordinates so a texture of a width shows so many of its pixels a meter.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="pixelsPerMeter">How many of the texture's pixels a meter of the faces shows.</param>
    /// <param name="texturePixels">How wide the texture is, in pixels.</param>
    /// <remarks>
    /// The density the faces have is measured over the selection as a whole, the square root of the
    /// area their coordinates cover over the area they cover in square meters, and one factor brings
    /// it to the one asked about their middle, so faces whose coordinates meet go on meeting. The
    /// recipe keeps the width, as <c>uv density 256 512</c>, so the line makes the same coordinates
    /// whether the texture is loaded or not.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The density is not above zero, or the width is below a pixel.</exception>
    /// <exception cref="InvalidOperationException">The selected faces' coordinates cover no area to measure.</exception>
    public static void EditMeshUvDensity(EditMesh mesh, float pixelsPerMeter, int texturePixels) =>
        Edited(mesh, nameof(EditMeshUvDensity))?.UvDensity(pixelsPerMeter, texturePixels);

    /// <summary>Scales the selected faces' texture coordinates so the texture of the first selected face's slot shows so many of its pixels a meter, and says whether it had one to read the width of.</summary>
    /// <remarks>
    /// The width is read from the slot's texture file, which <see cref="EditMeshSlotMap"/> gave it,
    /// and written to the recipe as <see cref="EditMeshUvDensity(EditMesh, float, int)"/> writes it.
    /// A slot with no texture, or one whose file cannot be read, leaves the coordinates as they were
    /// with a warning.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The density is not above zero.</exception>
    /// <exception cref="InvalidOperationException">The selected faces' coordinates cover no area to measure.</exception>
    public static bool EditMeshUvDensity(EditMesh mesh, float pixelsPerMeter)
    {
        if (Edited(mesh, nameof(EditMeshUvDensity)) is not { } data) return false;
        if (data.SelectedTextureWidth() is not { } width)
        {
            ApiLogger.Warn("EditMeshUvDensity: the first selected face's slot has no texture to read the width of, so the coordinates are left as they were.");
            return false;
        }
        data.UvDensity(pixelsPerMeter, width);
        return true;
    }

    /// <summary>Puts the selected faces' texture coordinates back on the world-aligned default.</summary>
    /// <remarks>The recipe's line is <c>uv reset</c>.</remarks>
    public static void EditMeshUvReset(EditMesh mesh) => Edited(mesh, nameof(EditMeshUvReset))?.UvReset();
}
