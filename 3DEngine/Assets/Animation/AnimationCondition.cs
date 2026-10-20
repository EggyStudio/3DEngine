namespace Engine;

/// <summary>When an <see cref="AnimationMachineTransition"/> is taken.</summary>
public enum AnimationCondition
{
    /// <summary>
    /// When the state's clip has played to its end since the state was entered, or come round to
    /// its start once for one that loops.
    /// </summary>
    Finished,

    /// <summary>When the parameter is true, any value but zero.</summary>
    True,

    /// <summary>When the parameter is false, or has not been set.</summary>
    False,

    /// <summary>When the parameter is above the transition's value.</summary>
    Above,

    /// <summary>When the parameter is below the transition's value.</summary>
    Below,

    /// <summary>When the parameter has been triggered, which taking the transition uses up.</summary>
    Triggered,
}
