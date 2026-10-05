// raylib's wave.fs, written in Slang: the texture sampled where a moving wave pushes each point.
import engine;

uniform float seconds;
uniform float2 size;
uniform float freqX;
uniform float freqY;
uniform float ampX;
uniform float ampY;
uniform float speedX;
uniform float speedY;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float pixelWidth = 1.0 / size.x;
    float pixelHeight = 1.0 / size.y;
    float aspect = pixelHeight / pixelWidth;
    float boxLeft = 0.0;
    float boxTop = 0.0;

    float2 p = input.uv;
    p.x += cos((input.uv.y - boxTop) * freqX / (pixelWidth * 750.0) + (seconds * speedX)) * ampX * pixelWidth;
    p.y += sin((input.uv.x - boxLeft) * freqY * aspect / (pixelHeight * 750.0) + (seconds * speedY)) * ampY * pixelHeight;

    return boundTexture.Sample(p) * input.color;
}
