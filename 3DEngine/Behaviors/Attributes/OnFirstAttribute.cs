namespace Engine;

/// <summary>Runs at the beginning of each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnFirstAttribute : Attribute;
