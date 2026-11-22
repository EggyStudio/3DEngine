namespace Engine;

/// <summary>An error in a CLI envelope, a code a script can branch on and a sentence a person can read.</summary>
internal readonly record struct CliError(string Code, string Message);
