namespace Engine;

/// <summary>One parameter of a command, for <c>e3d list</c>.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Kind">How the word is read: <c>text</c>, <c>flag</c>, <c>whole</c>, <c>long</c>, <c>single</c> or <c>number</c>.</param>
/// <param name="TakesLine">Whether the parameter takes the rest of the line.</param>
public readonly record struct CommandParameter(string Name, string Kind, bool TakesLine = false);
