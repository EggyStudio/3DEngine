// What a pass over a whole image needs: a triangle that covers it with no vertex buffer, and a
// tent filter that reads an image at a smaller size than it is drawn at without its texels showing.
module fullscreen;

public struct FullscreenOutput
{
    public float4 position : SV_Position;
    public float2 uv : TEXCOORD0;
};

// One triangle twice the target's size from three vertex indices, so its middle covers the target,
// with uv 0 at the top left and 1 at the bottom right.
public FullscreenOutput fullscreenTriangle(uint id)
{
    FullscreenOutput output;
    output.uv = float2((id << 1) & 2, id & 2);
    output.position = float4(output.uv * 2.0 - 1.0, 0.0, 1.0);
    return output;
}

// Nine taps a texel apart, weighted 1, 2, 1 along each axis, which smooths a lower level as it is
// scaled up where a single bilinear tap would leave its texels as blocks.
public float3 tent(Sampler2D image, float2 uv, float2 texel)
{
    float3 sum = image.SampleLevel(uv, 0).rgb * 4.0;
    sum += (image.SampleLevel(uv + float2(-texel.x, 0), 0).rgb + image.SampleLevel(uv + float2(texel.x, 0), 0).rgb
          + image.SampleLevel(uv + float2(0, -texel.y), 0).rgb + image.SampleLevel(uv + float2(0, texel.y), 0).rgb) * 2.0;
    sum += image.SampleLevel(uv - texel, 0).rgb + image.SampleLevel(uv + texel, 0).rgb
         + image.SampleLevel(uv + float2(-texel.x, texel.y), 0).rgb + image.SampleLevel(uv + float2(texel.x, -texel.y), 0).rgb;
    return sum / 16.0;
}
