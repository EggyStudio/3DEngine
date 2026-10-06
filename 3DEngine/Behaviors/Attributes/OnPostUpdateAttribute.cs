namespace Engine;

/// <summary>Runs after Update each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnPostUpdateAttribute : Attribute;
