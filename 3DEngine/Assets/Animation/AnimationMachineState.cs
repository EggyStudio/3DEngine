namespace Engine;

/// <summary>One state of an <see cref="AnimationStateMachine"/>, a clip of the model and how it plays.</summary>
public sealed class AnimationMachineState
{
    /// <summary>What transitions call it, unique in its machine.</summary>
    public string Name = "";

    /// <summary>The model's clip it plays, by the name the file gives it, or empty for the file's first.</summary>
    public string Clip = "";

    /// <summary>Whether the clip plays over and over, as a walk does, rather than once and holding its last frame.</summary>
    public bool Loop = true;

    /// <summary>How fast the clip plays, 1 as it was made.</summary>
    public float Speed = 1;
}
