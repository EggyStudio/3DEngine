namespace Engine;

/// <summary>Runs during the render stage each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnRenderAttribute : Attribute;
