namespace Engine;

/// <summary>Runs before Update each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnPreUpdateAttribute : Attribute;
