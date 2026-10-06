// raylib's dream_vision.fs, written in Slang: the picture smeared along a diagonal and made gray.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float4 color = texture0(fragTexCoord);

    color += texture0(fragTexCoord + 0.001);
    color += texture0(fragTexCoord + 0.003);
    color += texture0(fragTexCoord + 0.005);
    color += texture0(fragTexCoord + 0.007);
    color += texture0(fragTexCoord + 0.009);
    color += texture0(fragTexCoord + 0.011);

    color += texture0(fragTexCoord - 0.001);
    color += texture0(fragTexCoord - 0.003);
    color += texture0(fragTexCoord - 0.005);
    color += texture0(fragTexCoord - 0.007);
    color += texture0(fragTexCoord - 0.009);
    color += texture0(fragTexCoord - 0.011);

    color.rgb = float3((color.r + color.g + color.b)/3.0);
    color = color/9.5;

    return color;
}
