// A cascade of the world's probes drawn into the window's scene as small cubes, to be looked at
// (gi.show probes), each face the light the probe gathers from that side over pi, what a white
// surface facing that way would return, and a probe inside a mesh left out. The cubes are drawn a
// call for the cascade, an instance a probe and thirty-six corners a cube, from the probes' faces
// as the light that bounces left them, hidden by the scene as any mesh is.
import color;
import scenefield;

struct ProbesPush
{
    // World to clip space, the window's camera's.
    float4x4 viewProjection;
    // x: the cascade. y: its probes along each side. zw: unused.
    uint4 cascade;
    // x: the probes' spacing in cells. y: a cube's half side as a share of the spacing. zw: unused.
    float4 size;
};

[[vk::push_constant]]
ConstantBuffer<ProbesPush> push;

// Each probe's six faces, six probes' worth along x, a cascade's probes a stack along z.
[[vk::binding(0, 0)]]
Texture3D<float4> cubes;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

struct ProbeVertex
{
    float4 position : SV_Position;
    nointerpolation float4 light : COLOR0;
};

static const float Pi = 3.14159265;

// The face a cube's corner belongs to, +x, -x, +y, -y, +z and -z in turn, and its corners, two
// triangles a face.
static const float3 Axes[6] = { float3(1, 0, 0), float3(-1, 0, 0), float3(0, 1, 0), float3(0, -1, 0), float3(0, 0, 1), float3(0, 0, -1) };
static const float2 Quad[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

[shader("vertex")]
ProbeVertex vertexMain(uint id : SV_VertexID, uint instance : SV_InstanceID)
{
    uint probes = push.cascade.y;
    uint c = push.cascade.x;
    uint3 at = uint3(instance % probes, (instance / probes) % probes, instance / (probes * probes));
    float4 oc = fieldInfo.cascades[c].originAndCell;
    float spacing = oc.w * push.size.x;
    float3 middle = oc.xyz + (float3(at) + 0.5) * spacing;

    uint face = id / 6u;
    float3 n = Axes[face];
    float3 side = abs(n.y) > 0.5 ? float3(1, 0, 0) : float3(0, 1, 0);
    float3 up = cross(n, side);
    float2 q = Quad[id % 6u];
    float3 corner = n + side * q.x + up * q.y;

    ProbeVertex output;
    output.position = mul(push.viewProjection, float4(middle + corner * spacing * push.size.y, 1.0));
    output.light = cubes.Load(int4(int(face * probes + at.x), int(at.y), int(c * probes + at.z), 0));
    return output;
}

[shader("fragment")]
float4 fragmentMain(ProbeVertex input) : SV_Target
{
    if (input.light.a < 0.5)
        discard;
    return float4(linearToSrgbPastWhite(input.light.rgb / Pi), 1.0);
}
