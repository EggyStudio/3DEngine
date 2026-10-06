// raylib's skybox.vs and skybox.fs, written in Slang for the model pass: a cube around the camera,
// turned with the view and never moved by it, colored by the cube texture in the direction of
// each point. The cube is named cubemap here, since environmentMap is the model pass's own.
import modelpass;

uniform SamplerCube cubemap;
uniform int vflipped;
uniform int doGamma;

struct SkyOutput
{
    float4 position : SV_Position;
    float3 direction : TEXCOORD0;
};

[shader("vertex")]
SkyOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    // skybox.vs leaves the view's translation out, which here is the cube moved to the camera.
    float4x4 viewProjection = modelPush.viewProjection;
    float4 eye = eyeInWorld(viewProjection);
    SkyOutput output;
    output.direction = position;
    output.position = mul(viewProjection, float4(position + eye.xyz, 1.0));
    return output;
}

[shader("fragment")]
float4 fragmentMain(SkyOutput input) : SV_Target
{
    // The texel as its bytes, as OpenGL reads them, through a view that does not decode sRGB
    float3 color = vflipped != 0
        ? cubemap.Sample(float3(input.direction.x, -input.direction.y, input.direction.z)).rgb
        : cubemap.Sample(input.direction).rgb;

    if (doGamma != 0)
    {
        color = color / (color + float3(1.0));
        color = pow(color, float3(1.0 / 2.2));
    }

    // The bytes written as they are, or as linear light into the HDR frame
    return float4(unlit(srgbToLinear(color)), 1.0);
}
