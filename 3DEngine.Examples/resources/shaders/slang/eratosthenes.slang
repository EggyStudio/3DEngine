// raylib's eratosthenes.fs, written in Slang: the sieve of Eratosthenes, by ProfJski. Each box of a
// grid is a whole number, white when it is prime, and otherwise colored by the smallest number that
// divides it.
import engine;

float4 Colorizer(float counter, float maxSize)
{
    float normsize = counter / maxSize;

    float red = smoothstep(0.3, 0.7, normsize);
    float green = sin(3.14159 * normsize);
    float blue = 1.0 - smoothstep(0.0, 0.4, normsize);

    return float4(0.8 * red, 0.8 * green, 0.8 * blue, 1.0);
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = float4(1.0);

    // A grid of a thousand by a thousand, the pixels in each box one number
    float scale = 1000.0;
    int value = int(scale * floor(input.uv.y * scale) + floor(input.uv.x * scale));

    if ((value == 0) || (value == 1) || (value == 2)) return float4(1.0);

    for (int i = 2; i < max(2.0, sqrt(float(value)) + 1.0); i++)
    {
        if ((value - i * floor(float(value) / float(i))) == 0)
        {
            color = Colorizer(float(i), scale);
        }
    }

    return color;
}
