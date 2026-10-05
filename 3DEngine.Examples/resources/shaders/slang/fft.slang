// raylib's fft.fs, written in Slang: a bar for each of the spectrum's 512 bins, as tall as the
// bin's amplitude in the red of a texture a pixel high.
import engine;

uniform float2 iResolution;
Sampler2D iChannel0;

static const float4 BLACK = float4(0.0, 0.0, 0.0, 1.0);
static const float4 WHITE = float4(1.0, 1.0, 1.0, 1.0);
static const float FFT_ROW = 0.0;
static const float NUM_OF_BINS = 512.0;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragCoord = input.uv*iResolution;
    float cellWidth = iResolution.x/NUM_OF_BINS;
    float binIndex = floor(fragCoord.x/cellWidth);
    float localX = fmod(fragCoord.x, cellWidth);    // GLSL's mod, the same for what is above 0
    float barWidth = cellWidth - 1.0;
    float4 color = WHITE;

    if (localX <= barWidth)
    {
        float sampleX = (binIndex + 0.5)/NUM_OF_BINS;
        float2 sampleCoord = float2(sampleX, FFT_ROW);
        float amplitude = iChannel0.Sample(sampleCoord).r; // Only filled the red channel, all channels left open for alternative use

        if (input.uv.y < amplitude) color = BLACK;
    }

    return color;
}
