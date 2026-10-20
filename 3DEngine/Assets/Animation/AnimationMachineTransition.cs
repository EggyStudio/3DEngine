namespace Engine;

/// <summary>A way from one state of an <see cref="AnimationStateMachine"/> to another, taken when its condition holds.</summary>
public sealed class AnimationMachineTransition
{
    /// <summary>The state it leaves, by name, or empty for any state but <see cref="To"/>.</summary>
    /// <remarks>
    /// A transition from any state is how a hit or a fall interrupts whatever a character is doing,
    /// written once rather than from each state. It is not taken from the state it goes to, which
    /// would start that state's clip again every frame its condition holds.
    /// </remarks>
    public string From = "";

    /// <summary>The state it enters, by name.</summary>
    public string To = "";

    /// <summary>When it is taken.</summary>
    public AnimationCondition When;

    /// <summary>The parameter the condition reads, by name, which <see cref="AnimationCondition.Finished"/> needs none of.</summary>
    public string Parameter = "";

    /// <summary>What <see cref="AnimationCondition.Above"/> and <see cref="AnimationCondition.Below"/> compare the parameter with.</summary>
    public float Value;

    /// <summary>Seconds over which the state left fades into the one entered, or 0 to cut.</summary>
    public float Fade = 0.2f;
}
