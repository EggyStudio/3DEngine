namespace Engine;

/// <summary>How far a fetch of a scene pack has come.</summary>
/// <param name="Stage">The stage it is at.</param>
/// <param name="Bytes">The bytes of the stage done, the pack's fetched while downloading and checking, its files' written while unpacking.</param>
/// <param name="Total">The bytes the stage takes in all.</param>
public readonly record struct ScenePackProgress(ScenePackStage Stage, long Bytes, long Total)
{
    /// <summary>The share of the stage done, from nothing to one.</summary>
    public double Fraction => Total > 0 ? Math.Clamp((double)Bytes / Total, 0, 1) : Stage == ScenePackStage.Done ? 1 : 0;
}
