// Draws one cascade of the scene's distance field over the window as the field holds the scene.
// Each pixel's ray from the eye is clipped to the cascade's box and stepped through it by the
// distance each point holds, and where it comes within a tenth of a cell of a surface it is shaded
// by the field's slope there, a light from above and behind the eye, tinted by the cascade, and
// where it leaves the box the window's picture is left as it is.
import fullscreen;
import scenefield;

struct ViewPush
{
    // Clip space to the world.
    float4x4 inverseViewProjection;
    // xyz: the eye. w: the cascade drawn.
    float4 eyeAndCascade;
};

[[vk::push_constant]]
ConstantBuffer<ViewPush> push;

[[vk::binding(0, 0)]]
Sampler3D sceneField;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[shader("vertex")]
FullscreenOutput vertexMain(uint id : SV_VertexID)
{
    return fullscreenTriangle(id);
}

float3 worldAt(float2 uv, float depth)
{
    float4 world = mul(push.inverseViewProjection, float4(uv * 2.0 - 1.0, depth, 1.0));
    return world.xyz / world.w;
}

static const float3 Tints[8] = {
    float3(0.95, 0.75, 0.45), float3(0.55, 0.8, 0.95), float3(0.6, 0.9, 0.55), float3(0.9, 0.6, 0.85),
    float3(0.95, 0.95, 0.6), float3(0.6, 0.95, 0.9), float3(0.95, 0.6, 0.55), float3(0.75, 0.7, 0.95),
};

[shader("fragment")]
float4 fragmentMain(FullscreenOutput input) : SV_Target
{
    int cascade = int(push.eyeAndCascade.w);
    if (!fieldOn(fieldInfo) || cascade >= int(fieldInfo.counts.x))
        discard;
    float4 oc = fieldInfo.cascades[cascade].originAndCell;
    if (oc.w <= 0.0)
        discard;

    float3 eye = push.eyeAndCascade.xyz;
    float3 way = normalize(worldAt(input.uv, 0.5) - eye);
    // Where the ray is inside the box, half a cell in from each side.
    float3 low = oc.xyz + oc.w * 0.5;
    float3 high = oc.xyz + oc.w * (fieldInfo.counts.y - 0.5);
    float3 inverse = 1.0 / way;
    float3 a = (low - eye) * inverse, b = (high - eye) * inverse;
    float near = max(max(min(a.x, b.x), min(a.y, b.y)), min(a.z, b.z));
    float far = min(min(max(a.x, b.x), max(a.y, b.y)), max(a.z, b.z));
    if (far <= max(near, 0.0))
        discard;

    float t = max(near, 0.0);
    float cell = oc.w;
    for (int i = 0; i < 256 && t < far; i++)
    {
        float3 p = eye + way * t;
        float d = fieldDistanceIn(sceneField, fieldInfo, cascade, p);
        if (d < cell * 0.1)
        {
            float e = cell * 0.5;
            float3 g = float3(
                fieldDistanceIn(sceneField, fieldInfo, cascade, p + float3(e, 0, 0)) - fieldDistanceIn(sceneField, fieldInfo, cascade, p - float3(e, 0, 0)),
                fieldDistanceIn(sceneField, fieldInfo, cascade, p + float3(0, e, 0)) - fieldDistanceIn(sceneField, fieldInfo, cascade, p - float3(0, e, 0)),
                fieldDistanceIn(sceneField, fieldInfo, cascade, p + float3(0, 0, e)) - fieldDistanceIn(sceneField, fieldInfo, cascade, p - float3(0, 0, e)));
            float3 n = dot(g, g) > 1e-12 ? normalize(g) : -way;
            float3 light = normalize(float3(0.3, 1.0, 0.0) - way * 0.5);
            float shade = 0.3 + 0.7 * saturate(dot(n, light));
            return float4(Tints[cascade & 7] * shade, 1.0);
        }
        t += max(d, cell * 0.1);
    }
    discard;
    return float4(0.0);
}
