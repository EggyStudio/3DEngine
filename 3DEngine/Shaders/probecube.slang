// What the stages that filter a reflection probe's capture share: the way a cube texel looks, as
// Vulkan's cube lookup has it, the solid angle a texel covers, and the nine spherical harmonics of
// bands 0 to 2 in the order the model pass evaluates them (Y00, Y1-1, Y10, Y11, Y2-2, Y2-1, Y20,
// Y21, Y22).

// The direction a cube texel looks along, for face coordinates from -1 to 1.
public float3 cubeDirection(uint face, float u, float v)
{
    float3 d;
    switch (face)
    {
        case 0: d = float3(1.0, -v, -u); break;
        case 1: d = float3(-1.0, -v, u); break;
        case 2: d = float3(u, 1.0, v); break;
        case 3: d = float3(u, -1.0, -v); break;
        case 4: d = float3(u, -v, 1.0); break;
        default: d = float3(-u, -v, -1.0); break;
    }
    return normalize(d);
}

// Face coordinates from -1 to 1 of the middle of texel (x, y) of a face size texels wide.
public float2 faceCoordinates(uint2 texel, uint size)
{
    return (float2(texel) + 0.5) / float(size) * 2.0 - 1.0;
}

// The solid angle a texel of a face size texels wide covers at face coordinates (u, v), the
// texel's area on the cube over the cube of its distance from the middle.
public float texelSolidAngle(float2 uv, uint size)
{
    float side = 2.0 / float(size);
    return side * side / pow(1.0 + dot(uv, uv), 1.5);
}

// The nine harmonics in a direction.
public void harmonics(float3 d, out float y[9])
{
    y[0] = 0.282095;
    y[1] = 0.488603 * d.y;
    y[2] = 0.488603 * d.z;
    y[3] = 0.488603 * d.x;
    y[4] = 1.092548 * d.x * d.y;
    y[5] = 1.092548 * d.y * d.z;
    y[6] = 0.315392 * (3.0 * d.z * d.z - 1.0);
    y[7] = 1.092548 * d.x * d.z;
    y[8] = 0.546274 * (d.x * d.x - d.y * d.y);
}
