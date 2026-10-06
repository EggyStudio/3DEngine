// raylib's fisheye.fs, written in Slang: the picture as a lens of 178 degrees would take it.
import engine;

// raylib draws its render texture turned upright, so its texture coordinate's y runs up the
// screen where this one's runs down. The shader works in raylib's and samples in this one's.
float2 raylibCoord(float2 uv) { return float2(uv.x, 1.0 - uv.y); }
float4 texture0(float2 coord) { return boundTexture.Sample(float2(coord.x, 1.0 - coord.y)); }

static const float PI = 3.1415926535;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float2 fragTexCoord = raylibCoord(input.uv);
    float aperture = 178.0;
    float apertureHalf = 0.5*aperture*(PI/180.0);
    float maxFactor = sin(apertureHalf);

    float2 uv = float2(0);
    float2 xy = 2.0*fragTexCoord.xy - 1.0;
    float d = length(xy);

    if (d < (2.0 - maxFactor))
    {
        d = length(xy*maxFactor);
        float z = sqrt(1.0 - d*d);
        float r = atan2(d, z)/PI;
        float phi = atan2(xy.y, xy.x);

        uv.x = r*cos(phi) + 0.5;
        uv.y = r*sin(phi) + 0.5;
    }
    else
    {
        uv = fragTexCoord.xy;
    }

    return texture0(uv);
}
