namespace Engine;

/// <summary>Declares a named typed slot on a render graph node.</summary>
/// <param name="Name">Human-readable slot name.</param>
/// <param name="Type">The type of data this slot carries.</param>
public readonly record struct SlotInfo(string Name, SlotType Type);
