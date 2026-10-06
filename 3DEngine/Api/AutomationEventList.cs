namespace Engine;

/// <summary>A list automation events are recorded into and played from, as raylib's.</summary>
/// <remarks>
/// It is a class, so the list given to <see cref="Engine3D.SetAutomationEventList"/> is the one
/// recording adds to, as raylib's list given by its address is.
/// </remarks>
public sealed class AutomationEventList
{
    internal AutomationEventList(int capacity)
    {
        Capacity = capacity;
        Events = new AutomationEvent[capacity];
    }

    /// <summary>How many events it holds at most, 16384 as raylib's.</summary>
    public int Capacity { get; }

    /// <summary>How many events it holds.</summary>
    public int Count { get; internal set; }

    /// <summary>The events, the first <see cref="Count"/> of them recorded or loaded.</summary>
    public AutomationEvent[] Events { get; }

    internal void Add(AutomationEvent automationEvent)
    {
        if (Count < Capacity) Events[Count++] = automationEvent;
    }
}
