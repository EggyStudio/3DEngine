// A mesh whose every vertex carries four bytes of the program's own (MeshStream), read as CUSTOM0:
// a palette index in the low four bits and a shade in the eight above them, unpacked here into the
// color the model pass lights. The bytes are a uint, whose bits are read as they were written, where
// a float's would be read as a float.
import modelpass;

static const float3 Palette[16] = {
    float3(0.90, 0.35, 0.20), float3(0.95, 0.55, 0.15), float3(0.95, 0.80, 0.20), float3(0.60, 0.85, 0.25),
    float3(0.25, 0.75, 0.35), float3(0.15, 0.70, 0.60), float3(0.20, 0.55, 0.85), float3(0.30, 0.35, 0.90),
    float3(0.55, 0.30, 0.85), float3(0.80, 0.30, 0.75), float3(0.90, 0.30, 0.50), float3(0.75, 0.75, 0.75),
    float3(0.55, 0.45, 0.35), float3(0.35, 0.50, 0.40), float3(0.40, 0.40, 0.50), float3(0.95, 0.95, 0.90),
};

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, uint packed : CUSTOM0,
    ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    float shade = ((packed >> 4) & 0xff) / 255.0;
    output.color = float4(Palette[packed & 0xf] * shade, 1);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    return float4(lit(input.color.rgb, input), 1);
}
