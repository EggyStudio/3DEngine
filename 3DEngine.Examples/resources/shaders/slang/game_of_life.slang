// raylib's game_of_life.fs, written in Slang: one generation of Conway's Game of Life, a black
// pixel alive and any other dead, from the world drawn across the whole target.
import engine;

// Input size in pixels of the textures
uniform float2 resolution;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // Size of one pixel in texture coordinates (from 0.0 to 1.0)
    float x = 1.0/resolution.x;
    float y = 1.0/resolution.y;
    float2 uv = input.uv;

    // Status of the current cell (1 = alive, 0 = dead)
    int origValue = (boundTexture.Sample(uv).r < 0.1)? 1 : 0;

    // Sum of alive neighbors
    int sumValue = (boundTexture.Sample(float2(uv.x - x, uv.y - y)).r < 0.1)? 1 : 0;    // Top-left
    sumValue    += (boundTexture.Sample(float2(uv.x - x, uv.y    )).r < 0.1)? 1 : 0;    // Top
    sumValue    += (boundTexture.Sample(float2(uv.x - x, uv.y + y)).r < 0.1)? 1 : 0;    // Top-right

    sumValue    += (boundTexture.Sample(float2(uv.x,     uv.y - y)).r < 0.1)? 1 : 0;    // Left
    sumValue    += (boundTexture.Sample(float2(uv.x,     uv.y + y)).r < 0.1)? 1 : 0;    // Right

    sumValue    += (boundTexture.Sample(float2(uv.x + x, uv.y - y)).r < 0.1)? 1 : 0;    // Bottom-left
    sumValue    += (boundTexture.Sample(float2(uv.x + x, uv.y    )).r < 0.1)? 1 : 0;    // Bottom
    sumValue    += (boundTexture.Sample(float2(uv.x + x, uv.y + y)).r < 0.1)? 1 : 0;    // Bottom-right

    // Game of life rules:
    // Current cell remains alive when 2 or 3 neighbors are alive, dies otherwise
    // Current cell goes from dead to alive when exactly 3 neighbors are alive
    if (((origValue == 1) && (sumValue == 2)) || sumValue == 3)
        return float4(0.0, 0.0, 0.0, 1.0);     // Alive: draw the pixel black
    else
        return input.color;                     // Dead: draw the pixel with the background color, RAYWHITE
}
