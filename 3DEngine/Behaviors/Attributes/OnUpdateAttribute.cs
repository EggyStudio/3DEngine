namespace Engine;

/// <summary>Runs during the main update stage each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnUpdateAttribute : Attribute;
