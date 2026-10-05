// raylib's vertex_displacement.vs and vertex_displacement.fs, written in Slang for the model pass:
// each vertex raised by a noise texture read where the time moves it, and colored from dark blue to
// white by its height.
import modelpass;

uniform float time;

Sampler2D perlinNoiseMap;

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    // Calculate animated texture coordinates based on time and vertex position
    float2 animatedTexCoord = sin(uv + float2(sin(time + position.x*0.1), cos(time + position.z*0.1))*0.3);

    // Normalize animated texture coordinates to range [0, 1]
    animatedTexCoord = animatedTexCoord*0.5 + 0.5;

    // Fetch displacement from the perlin noise map, at its one level, since a vertex has no
    // neighbors to pick a level by
    float displacement = perlinNoiseMap.SampleLevel(animatedTexCoord, 0).r*7; // Amplified displacement

    // Displace vertex position
    float3 displacedPosition = position + float3(0.0, displacement, 0.0);

    return transformModelVertex(displacedPosition, normal, uv, instance);
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The height raylib's vertex stage hands on, which is the world's here, the plane being drawn
    // where it was made
    float height = input.world.y*0.2;

    float4 darkblue = float4(0.0, 0.13, 0.18, 1.0);
    float4 lightblue = float4(1.0, 1.0, 1.0, 1.0);
    // Interpolate between two colors based on height
    return lerp(darkblue, lightblue, height);
}
