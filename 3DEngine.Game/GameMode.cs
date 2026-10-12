namespace Engine.Game;

/// <summary>How the player plays, as Minecraft's modes are.</summary>
public enum GameMode
{
    /// <summary>Every block to place without end, blocks broken at once and dropping nothing, and flight, for building a test of the light.</summary>
    Creative,

    /// <summary>Blocks that take time to break and drop what they are made of, an inventory that placing uses up, and no flight.</summary>
    Survival,
}
