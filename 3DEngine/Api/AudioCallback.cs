namespace Engine;

/// <summary>Fills a buffer with an audio stream's next samples, interleaved, from -1 to 1.</summary>
public delegate void AudioCallback(Span<float> samples);
