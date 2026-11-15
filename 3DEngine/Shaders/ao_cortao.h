// What CORTAO's three passes (cortao/) read of the window's depth at half its size, shared by
// ao_cortao_prepass.slang, ao_cortao_main.slang and ao_cortao_filter.slang. The engine draws no
// buffer of normals, so each pass takes a pixel's normal in view space from the depth around it,
// as ao.slang does in the world.

// A depth of the window's projection to its distance along the view, as CORTAO's prepass takes it,
// 1 / (depth * scale + offset).
float cortaoLinear(float d, float scale, float offset)
{
    return 1.0 / (d * scale + offset);
}

// The point in view space at a pixel of the depth, through CORTAO's unprojection.
float3 cortaoViewAt(Sampler2D depth, int2 pixel, uint2 size, float4 unproject, float scale, float offset)
{
    float d = depth.Load(int3(clamp(pixel, int2(0, 0), int2(size) - 1), 0)).r;
    float2 uv = (float2(pixel) + 0.5) / float2(size);
    float2 way = uv * unproject.xy + unproject.zw;
    return float3(way, -1.0) * cortaoLinear(d, scale, offset);
}

// The surface's normal in view space at a pixel, from the nearer of the pixels on either side
// along each axis, so an edge does not lean it toward what is behind, turned to face the eye.
float3 cortaoNormalAt(Sampler2D depth, int2 pixel, uint2 size, float4 unproject, float scale, float offset)
{
    float3 p = cortaoViewAt(depth, pixel, size, unproject, scale, offset);
    float3 right = cortaoViewAt(depth, pixel + int2(1, 0), size, unproject, scale, offset) - p;
    float3 left = p - cortaoViewAt(depth, pixel - int2(1, 0), size, unproject, scale, offset);
    float3 down = cortaoViewAt(depth, pixel + int2(0, 1), size, unproject, scale, offset) - p;
    float3 up = p - cortaoViewAt(depth, pixel - int2(0, 1), size, unproject, scale, offset);
    float3 across = dot(right, right) < dot(left, left) ? right : left;
    float3 along = dot(down, down) < dot(up, up) ? down : up;
    float3 n = normalize(cross(across, along));
    return dot(n, -p) < 0.0 ? -n : n;
}
