// Color conversions every pass that writes the frame agrees on: sRGB's transfer functions and the
// tonemap that brings light past 1 under it. modelpass exports them to model shaders, and the
// composite that brings the window's HDR frame into the window uses the same ones, so a render
// texture's scene looks as the window's does.
module color;

// sRGB's transfer function, exactly rather than as a power of 2.2, so a byte decoded and encoded
// again comes back as itself.
public float3 srgbToLinear(float3 c)
{
    return select(c <= 0.04045, c / 12.92, pow((max(c, 0.0) + 0.055) / 1.055, 2.4));
}

public float3 linearToSrgb(float3 c)
{
    return linearToSrgbPastWhite(saturate(c));
}

// The same encoding carried on past 1, as the window's HDR frame holds light brighter than white,
// which srgbToLinear brings back as it was.
public float3 linearToSrgbPastWhite(float3 c)
{
    c = max(c, 0.0);
    return select(c <= 0.0031308, c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055);
}

// Brings light past 1 back under it. The brightest channel is left alone up to 0.9 and bends
// smoothly toward 1 past it, and the other two are scaled with it, so a bright orange light stays
// orange where a clamp per channel turns it white. Below the bend the color is unchanged.
public float3 tonemap(float3 color)
{
    static const float knee = 0.9;
    float peak = max(color.r, max(color.g, color.b));
    if (peak <= knee)
        return max(color, 0.0);
    float bent = knee + (1.0 - knee) * (1.0 - exp(-(peak - knee) / (1.0 - knee)));
    return max(color, 0.0) * (bent / peak);
}
