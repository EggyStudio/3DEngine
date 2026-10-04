// What the immediate pass gives every shader drawn with it, imported by immediate.slang and by any
// shader a program loads with LoadShader:
//
//     import engine;
//
//     [shader("fragment")]
//     float4 fragmentMain(VertexOutput input) : SV_Target
//     {
//         float4 color = input.color * boundTexture.Sample(input.uv);
//         return float4(color.rgb * param(0).x, color.a);
//     }
//
// param(0) to param(3) are the four float4 slots SetShaderValue fills. Uniforms the shader declares
// at the top level, as `uniform float4 tint;`, are set by name, through GetShaderLocation and
// SetShaderValue, and hold for what is drawn after they are set. A texture declared the same way,
// as `Sampler2D detail;`, is set with SetShaderValueTexture.
module engine;

public struct PushConstants
{
    public float4x4 transform;
    public float4 params[4];
};

[[vk::push_constant]]
public ConstantBuffer<PushConstants> push;

[[vk::binding(1, 0)]]
public Sampler2D boundTexture;

public struct VertexOutput
{
    public float4 position : SV_Position;
    public float2 uv : TEXCOORD0;
    public float4 color : COLOR0;
};

// One of the four float4 values SetShaderValue sets, by slot.
public float4 param(int slot)
{
    return push.params[slot];
}

// The vertex stage every immediate shader uses unless it writes its own.
public VertexOutput transformVertex(float3 position, float2 uv, float4 color)
{
    VertexOutput output;
    output.position = mul(push.transform, float4(position, 1.0));
    output.uv = uv;
    output.color = color;
    return output;
}
