namespace Engine;

/// <summary>A thing that happened in a frame, recorded to be played back, as raylib's automation event.</summary>
/// <param name="Frame">The frame it happened in, counted from the base frame recording started at.</param>
/// <param name="Type">What happened.</param>
/// <param name="Param0">The first of what it needs, as <see cref="AutomationEventType"/> says for each type.</param>
/// <param name="Param1">The second.</param>
/// <param name="Param2">The third.</param>
/// <param name="Param3">The fourth, which no type uses.</param>
public readonly record struct AutomationEvent(int Frame, AutomationEventType Type, int Param0 = 0, int Param1 = 0, int Param2 = 0, int Param3 = 0);
