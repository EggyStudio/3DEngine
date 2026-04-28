// Polyfills required to use modern C# features (records, init-only setters,
// required members, ...) when targeting netstandard2.0 (Roslyn analyzer TFM).

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }