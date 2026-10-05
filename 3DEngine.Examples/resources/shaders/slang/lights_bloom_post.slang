// raylib's lights_bloom_post.fs, written in Slang: what is brighter than 0.75 in any channel
// blurred over its neighbors and added back, then tone mapped by Reinhard's curve.
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = boundTexture.Sample(input.uv);

    // High-pass threshold blur: only pixels brighter than 0.75 in any channel contribute
    float4 bloom = float4(0.0);
    float2 texel = float2(1.0/1280.0, 1.0/720.0)*2.5;

    for (int x = -2; x <= 2; x++)
    {
        for (int y = -2; y <= 2; y++)
        {
            float4 smp = boundTexture.Sample(input.uv + float2(float(x), float(y))*texel);
            float brightness = max(smp.r, max(smp.g, smp.b));
            if (brightness > 0.75) bloom += smp;
        }
    }
    bloom /= 25.0;

    // Combine original with the bloom glow, then Reinhard tone-map back to [0, 1]
    float3 hdr = color.rgb + bloom.rgb*1.2;
    float3 ldr = hdr/(hdr + float3(1.0));

    return float4(ldr, color.a);
}
