namespace Engine;

/// <summary>
/// The part of a texture <see cref="Engine3D.DrawTextureNPatch"/> draws, and how far in from each
/// of its edges the borders that keep their size reach, in pixels.
/// </summary>
public readonly record struct NPatchInfo(Rectangle Source, int Left, int Top, int Right, int Bottom, NPatchLayout Layout = NPatchLayout.NinePatch);
