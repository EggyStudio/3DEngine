// raylib's cubes_panning.fs, written in Slang: a grid of squares that pans and turns a quarter in
// each second, drawn over whatever texture is given, which only carries the coordinates.
import engine;

static const float PI = 3.14159265358979323846;

uniform float uTime;

static const float divisions = 5.0;

// A point of a cell turned about the cell's middle, by an angle that rests, turns and rests again
// each second of the time scaled by speed.
float2 VectorRotateTime(float2 v, float speed)
{
    float time = uTime * speed;
    float localTime = frac(time);
    float angle = 0.0;

    if ((localTime >= 0.0) && (localTime < 0.25)) angle = 0.0;
    else if ((localTime >= 0.25) && (localTime < 0.50)) angle = PI / 4 * sin(2 * PI * localTime - PI / 2);
    else if ((localTime >= 0.50) && (localTime < 0.75)) angle = PI * 0.25;
    else if ((localTime >= 0.75) && (localTime < 1.00)) angle = PI / 4 * sin(2 * PI * localTime);

    // GLSL's mat2(cos, -sin, sin, cos) times v, its columns written out
    v -= 0.5;
    v = float2(cos(angle) * v.x + sin(angle) * v.y, -sin(angle) * v.x + cos(angle) * v.y);
    v += 0.5;
    return v;
}

// 1 inside a square of the size centered in the cell, 0 outside
float Rectangle(float2 st, float size, float fill)
{
    float roundSize = 0.5 - size / 2.0;
    float left = step(roundSize, st.x);
    float top = step(roundSize, st.y);
    float bottom = step(roundSize, 1.0 - st.y);
    float right = step(roundSize, 1.0 - st.x);
    return (left * bottom * right * top) * fill;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragPos = input.uv;
    fragPos += uTime / 9.0;
    fragPos *= divisions;

    float2 fpos = frac(fragPos);
    fpos = VectorRotateTime(fpos, 0.2);

    float alpha = Rectangle(fpos, 0.216, 1.0);
    float3 color = float3(0.3, 0.3, 0.3);

    return float4(color, alpha);
}
