// raylib's ascii.fs, written in Slang: the picture in cells, each a character as bright as the cell.
// The character set is from https://www.shadertoy.com/view/lssGDj.
import engine;

uniform float2 resolution;

// Below 9 the characters may not be whole.
uniform float fontSize;

float GreyScale(float3 col)
{
    return dot(col, float3(0.2126, 0.7152, 0.0722));
}

// Whether a point of the cell, from -1 to 1 each way, is on in the 5 by 5 bitmap n.
float GetCharacter(int n, float2 p)
{
    p = floor(p * float2(-4.0, 4.0) + 2.5);

    if (clamp(p.x, 0.0, 4.0) == p.x && clamp(p.y, 0.0, 4.0) == p.y)
    {
        int a = int(round(p.x) + 5.0 * round(p.y));
        if (((n >> a) & 1) == 1) return 1.0;
    }

    // Off, or outside the grid
    return 0.0;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // Cells of the font's size
    float2 charPixelSize = float2(fontSize, fontSize);
    float2 uvCellSize = charPixelSize / resolution;

    float2 cellUV = floor(input.uv / uvCellSize) * uvCellSize;
    float3 cellColor = boundTexture.Sample(cellUV).rgb;

    // The cell's brightness picks its character.
    float gray = GreyScale(cellColor);

    int n = 4096;
    if (gray > 0.2) n = 65600;    // :
    if (gray > 0.3) n = 18725316; // v
    if (gray > 0.4) n = 15255086; // o
    if (gray > 0.5) n = 13121101; // &
    if (gray > 0.6) n = 15252014; // 8
    if (gray > 0.7) n = 13195790; // @
    if (gray > 0.8) n = 11512810; // #

    // raylib's target is stored from the bottom and drawn turned, so its texture coordinates rise
    // up the screen, and here they fall. The character's rows are counted up, as raylib's are.
    float2 localUV = (input.uv - cellUV) / uvCellSize;
    float2 p = localUV * 2.0 - 1.0;
    p.y = -p.y;

    float3 color = cellColor * GetCharacter(n, p);
    return float4(color, 1.0);
}
