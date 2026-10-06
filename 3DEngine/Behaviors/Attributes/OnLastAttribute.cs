namespace Engine;

/// <summary>Runs at the very end of each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnLastAttribute : Attribute;
