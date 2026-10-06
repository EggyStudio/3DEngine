namespace Engine;

/// <summary>How <see cref="Engine3D.DrawTextureNPatch"/> cuts a texture: into nine patches, or three across or down.</summary>
public enum NPatchLayout
{
    /// <summary>Corners kept at their size, edges stretched along their length and the middle both ways.</summary>
    NinePatch,
    /// <summary>A top and a bottom kept at their height, and the part between stretched down.</summary>
    ThreePatchVertical,
    /// <summary>A left and a right kept at their width, and the part between stretched across.</summary>
    ThreePatchHorizontal,
}
