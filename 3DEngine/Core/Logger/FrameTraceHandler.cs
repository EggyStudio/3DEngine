using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// The text of an interpolated <see cref="ILogger.FrameTrace(ref FrameTraceHandler)"/>, built only
/// while <see cref="LogConfig.PerFrameLogging"/> is on. While it is off the holes are not evaluated
/// and no string is made, so a trace written in every frame costs a frame nothing.
/// </summary>
[InterpolatedStringHandler]
public ref struct FrameTraceHandler
{
    private DefaultInterpolatedStringHandler _text;

    /// <summary>Begins the text, and says whether it is wanted.</summary>
    /// <param name="literalLength">The length of the text's literal parts.</param>
    /// <param name="formattedCount">The number of holes.</param>
    /// <param name="wanted">
    /// Whether per-frame logging is on, which the holes are evaluated only if.
    /// </param>
    public FrameTraceHandler(int literalLength, int formattedCount, out bool wanted)
    {
        Wanted = wanted = LogConfig.PerFrameLogging;
        if (wanted) _text = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
    }

    /// <summary>Whether the text was wanted when it began.</summary>
    public bool Wanted { get; }

    /// <summary>Appends a literal part.</summary>
    /// <param name="value">The literal.</param>
    public void AppendLiteral(string value) => _text.AppendLiteral(value);

    /// <summary>Appends a hole's value.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    public void AppendFormatted<T>(T value) => _text.AppendFormatted(value);

    /// <summary>Appends a hole's value in a format.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="format">The format.</param>
    public void AppendFormatted<T>(T value, string? format) => _text.AppendFormatted(value, format);

    /// <summary>The text, which the handler gives up.</summary>
    /// <returns>The text built.</returns>
    public string ToStringAndClear() => _text.ToStringAndClear();
}
