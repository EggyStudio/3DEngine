namespace Engine;

/// <summary>Runs once during app cleanup, after the window loop ends.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnCleanupAttribute : Attribute;
