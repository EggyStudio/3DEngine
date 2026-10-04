// FXAA over the composited frame, in its encoded colors. Where the brightness changes across a
// pixel, the pixel is blended along the edge rather than across it, which smooths what
// multisampling leaves, the edges inside a surface and of lines thinner than a pixel.
import fullscreen;

struct FxaaPush
{
    // xy: one texel of the frame in uv.
    float4 texel;
};

[[vk::push_constant]]
ConstantBuffer<FxaaPush> push;

[[vk::binding(0, 0)]]
Sampler2D frame;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float luma(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

float3 at(float2 uv)
{
    return frame.SampleLevel(uv, 0).rgb;
}

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    static const float spanMax = 8.0;
    static const float reduceMul = 1.0 / 8.0;
    static const float reduceMin = 1.0 / 128.0;
    float2 t = push.texel.xy;
    float3 middle = at(input.uv);
    float nw = luma(at(input.uv + float2(-1, -1) * t)), ne = luma(at(input.uv + float2(1, -1) * t));
    float sw = luma(at(input.uv + float2(-1, 1) * t)), se = luma(at(input.uv + float2(1, 1) * t));
    float m = luma(middle);
    float lowest = min(m, min(min(nw, ne), min(sw, se)));
    float highest = max(m, max(max(nw, ne), max(sw, se)));

    // The edge's direction from the four corners, along which the blend is taken.
    float2 along = float2(-((nw + ne) - (sw + se)), (nw + sw) - (ne + se));
    float reduce = max((nw + ne + sw + se) * 0.25 * reduceMul, reduceMin);
    float scale = 1.0 / (min(abs(along.x), abs(along.y)) + reduce);
    along = clamp(along * scale, -spanMax, spanMax) * t;

    float3 near = 0.5 * (at(input.uv + along * (1.0 / 3.0 - 0.5)) + at(input.uv + along * (2.0 / 3.0 - 0.5)));
    float3 far = near * 0.5 + 0.25 * (at(input.uv - along * 0.5) + at(input.uv + along * 0.5));
    float l = luma(far);
    return float4(l < lowest || l > highest ? near : far, 1.0);
}
