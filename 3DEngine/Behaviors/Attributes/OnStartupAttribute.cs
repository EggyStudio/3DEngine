namespace Engine;

/// <summary>Runs once during app startup, before the window loop begins.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnStartupAttribute : Attribute;
