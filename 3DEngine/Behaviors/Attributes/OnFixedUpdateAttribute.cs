namespace Engine;

/// <summary>Runs at a fixed rate, zero or more times a frame, between pre-update and update (see <see cref="FixedTime"/>).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnFixedUpdateAttribute : Attribute;
