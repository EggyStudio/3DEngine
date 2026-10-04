namespace Engine;

/// <summary>
/// A model file an entity shows and plays a clip of, as <c>DrawModel</c> and
/// <c>UpdateModelAnimationAt</c> do for the flat API, placed by the entity's
/// <see cref="Transform"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each entity loads a copy of the model of its own, so entities showing the same file are posed
/// apart, and lets it go when the component or the entity goes. It is drawn through the first
/// entity with a <see cref="Camera"/>, as mesh entities are, by <see cref="AnimatedModelDraws"/>,
/// which advances <see cref="Time"/> by <see cref="Speed"/> each frame.
/// </para>
/// <para>
/// Setting <see cref="Clip"/> to another clip blends from the one playing over
/// <see cref="BlendSeconds"/>, as a walk turns into a run. A component made with <c>default</c>
/// has a speed of 0 and stands still, so <see cref="AnimatedModel(string, string, float, float)"/>
/// is the one to make.
/// </para>
/// </remarks>
[SceneComponent]
public struct AnimatedModel
{
    /// <summary>The model file, relative to the program, with its clips in it.</summary>
    public string Path;

    /// <summary>
    /// The name of the clip playing, or empty for the file's first. A change keeps
    /// <see cref="Time"/>, so a clip meant to start from its beginning is given a time of 0 with it.
    /// </summary>
    public string Clip;

    /// <summary>How fast the clip plays, 1 as it was made, 0 held still, below 0 backward.</summary>
    public float Speed;

    /// <summary>How far into the clip it is, in seconds, counted round its length. Set to jump.</summary>
    public float Time;

    /// <summary>How long a change of clip takes to blend from the one before, in seconds, or 0 to cut.</summary>
    public float BlendSeconds;

    /// <summary>A model playing a clip from its start.</summary>
    public AnimatedModel(string path = "", string clip = "", float speed = 1, float blendSeconds = 0.2f)
    {
        Path = path;
        Clip = clip;
        Speed = speed;
        Time = 0;
        BlendSeconds = blendSeconds;
    }
}
