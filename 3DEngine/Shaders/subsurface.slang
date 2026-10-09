// The window's meshes whose material scatters light under its surface, drawn again after the scene
// into two images of the window's size, one sample a pixel: the light each pixel scatters
// diffusely, as the model pass worked it out, with 1 in alpha, and the material's profile, the
// share of its radius each of red, green and blue travels under the surface, and the radius.
// subsurface_blur.slang then spreads that light over the frame. A fragment is drawn only where the
// scene's depth shows its surface, so a marked surface behind another, a cheek behind a hand or
// a shape drawn in front, leaves its pixels unmarked, and a pixel the scene shows of an unmarked
// surface is never touched. The mesh's colors and second texture coordinates are read as the
// model pass reads them, from the mesh or the one default for every vertex.
import modelpass;

// The scene's depth as its pass left it, its first sample's where the window has several.
[[vk::binding(0, 2)]]
Sampler2D sceneDepth;

struct SubsurfaceVertexOutput
{
    ModelStreamsOutput streams;
    // The material's profile, the same for every instance of the call.
    nointerpolation float4 profile : TEXCOORD5;
};

struct Marked
{
    float4 diffuse : SV_Target0;
    float4 profile : SV_Target1;
};

[shader("vertex")]
SubsurfaceVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance,
    [[vk::location(9)]] float4 color : COLOR0, [[vk::location(10)]] float2 uv2 : TEXCOORD1, [[vk::location(11)]] float4 profile : TEXCOORD2)
{
    SubsurfaceVertexOutput output;
    output.streams = transformModelVertex(position, normal, uv, color, uv2, instance);
    output.profile = profile;
    return output;
}

[shader("fragment")]
Marked fragmentMain(SubsurfaceVertexOutput input)
{
    ModelVertexOutput base = input.streams.base;
    // How far depth changes across a pixel here, worked out before any fragment is dropped.
    float slope = fwidth(base.position.z);
    uint width, height;
    sceneDepth.GetDimensions(width, height);
    float scene = sceneDepth.SampleLevel(base.position.xy / float2(width, height), 0).r;
    float4 color = alphaTested(baseColor(input.streams), base);

    Marked output;
    output.diffuse = float4(0.0);
    output.profile = float4(0.0);
    // Unlit, as raylib draws a model before a program makes a light, there is no light to scatter.
    if (!drawnUnlit(base))
    {
        LitLight light = litLight(color.rgb, base);
        float exposure = lighting.environment.w > 0.0 ? lighting.environment.w : 1.0;
        output.diffuse = float4(light.diffuse * exposure, 1.0);
        output.profile = input.profile;
    }
    // The scene's depth is its first sample's, a little off the pixel's middle, so the surface the
    // scene shows lies within two pixels' change of the fragment's, and one nearer hides it.
    if (scene < base.position.z - 2.0 * slope - 1e-6)
        discard;
    return output;
}
