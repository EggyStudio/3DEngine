// Draws the environment map as a backdrop, on a cube around the camera that DrawSkybox records.
// Each pixel looks the sky up along the way from the eye through it, so the cube's size and place
// never show, and its depth is set a millionth inside the far plane, so everything else draws in front.
import modelpass;

// The color SetSkyHorizon draws below the horizon, sRGB, with 1 in w where there is one, and in x
// of the second the degrees above the horizon over which the image blends into it.
uniform float4 horizonColor;
uniform float4 horizonBlend;

// The sun's disc a day sky with air draws over its own sky, xyz the way toward it and w the cosine
// of its angular radius, or 0 in w for none, and in the second its luminance in the frame's light.
uniform float4 sunDisc;
uniform float4 sunLight;

// The moon's disc a day sky with air draws, xyz the way toward it and w the cosine of its angular
// radius, or 0 in w for none, in the second its sunlit part's luminance in the frame's light and
// in w its dark part's, the earthshine, and in the third the way toward the sun its lit side faces.
uniform float4 moonDisc;
uniform float4 moonLight;
uniform float4 moonSun;

// The night's stars and the Milky Way's band, which a day sky with air draws by their luminance, in
// the first x and y the sine and cosine of how far the stars have turned (DaySky.SiderealTurn), z
// what a luminance is multiplied by for the frame, 0 for none, and w the cells along each side of
// the stars' cube (DaySky.StarCells), and in the second the air's optical depth at the zenith in
// rgb and in w how many stars the buffer holds.
uniform float4 nightSky;
uniform float4 nightAir;

// The bright stars, the cube's cells' first stars and then each star's way, illuminance and color
// (DaySky.StarGrid).
StructuredBuffer<uint> stars;

// The way in the stars' coordinates, right ascension along x toward y and declination toward z,
// of a way in the world, the sky turned by the sidereal time's sine s and cosine c, as
// DaySky.TowardStars takes it out.
float3 toStars(float3 w, float s, float c)
{
    return float3(-s * w.x + c * w.y, c * w.x + s * w.y, -w.z);
}

// The cell of the stars' cube a way in their coordinates falls in, as DaySky.CellOf finds it, and
// its face and place across the face in cells.
int starCell(float3 e, int cells, out int face, out int2 at)
{
    float3 a = abs(e);
    float major, u, v;
    if (a.x >= a.y && a.x >= a.z) { face = e.x > 0.0 ? 0 : 1; major = a.x; u = e.y; v = e.z; }
    else if (a.y >= a.z) { face = e.y > 0.0 ? 2 : 3; major = a.y; u = e.x; v = e.z; }
    else { face = e.z > 0.0 ? 4 : 5; major = a.z; u = e.x; v = e.y; }
    at = clamp(int2((float2(u, v) / major * 0.5 + 0.5) * float(cells)), int2(0), int2(cells - 1));
    return (face * cells + at.y) * cells + at.x;
}

// The stars' light along way e, in candelas a square meter outside the air, each star of its cell
// and the eight around it on the same face as a point some two pixels wide, a Gaussian whose sigma
// is 0.8 of the pixel's angle across, holding the star's illuminance over the steradians it covers.
// Read at each pixel's middle, its brightest pixel holds a quarter of its light with the star on
// that middle and a sixth with it on a corner, so a star the sky turns across the pixels keeps
// within a factor of 1.5, where at half a pixel it would swing by 2.7.
float3 starLight(float3 e, float pixel, int cells, uint count)
{
    int face;
    int2 at;
    starCell(e, cells, face, at);
    int head = (6 * cells * cells + 1 + 3) / 4 * 4;
    float sigma = max(pixel * 0.8, 1e-6);
    float3 sum = float3(0.0);
    for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 near = at + int2(dx, dy);
            if (any(near < int2(0)) || any(near >= int2(cells)))
                continue;
            int cell = (face * cells + near.y) * cells + near.x;
            // Every read is held within the stars, since a draw that goes without its buffer
            // reads another bound in its place, as the model pass's sixteen bytes, and with no
            // robust access the cells' ends read past it are whatever lies beyond, loops long
            // enough to lose the device.
            uint first = min(stars[cell], count);
            uint last = min(stars[cell + 1], count);
            for (uint k = first; k < last; k++)
            {
                uint base = uint(head) + k * 8u;
                float3 way = float3(asfloat(stars[base]), asfloat(stars[base + 1u]), asfloat(stars[base + 2u]));
                // The chord between the two ways stands for their angle, near the star the same,
                // where acos of their dot gives it in steps of 3.5e-4 radians, a float's last bit
                // short of 1, a third of a pixel in a frame 4 degrees high.
                float3 apart = way - e;
                float spread = exp(-0.5 * dot(apart, apart) / (sigma * sigma)) / (6.28318531 * sigma * sigma);
                sum += asfloat(stars[base + 3u]) * spread * float3(asfloat(stars[base + 4u]), asfloat(stars[base + 5u]), asfloat(stars[base + 6u]));
            }
        }
    return sum;
}

// The Milky Way's luminance along way e in the stars' coordinates, in candelas a square meter
// outside the air, as the Sky module's Night.GalaxyLuminance gives it, 100 S10 in the galactic plane
// toward the anticenter and 1,000 toward the center, a third each 8 degrees off it, an S10 a
// tenth-magnitude star a square degree, 8.33e-7 candelas a square meter.
float galaxyLight(float3 e)
{
    float ra = atan2(e.y, e.x), dec = asin(clamp(e.z, -1.0, 1.0));
    float poleRa = radians(192.859), poleDec = radians(27.128), poleLongitude = radians(122.932);
    float sinB = sin(poleDec) * sin(dec) + cos(poleDec) * cos(dec) * cos(ra - poleRa);
    float b = asin(clamp(sinB, -1.0, 1.0));
    float y = cos(dec) * sin(ra - poleRa);
    float x = cos(poleDec) * sin(dec) - sin(poleDec) * cos(dec) * cos(ra - poleRa);
    float l = poleLongitude - atan2(y, x);
    float toCenter = pow((1.0 + cos(l)) * 0.5, 3.0);
    return (100.0 + 900.0 * toCenter) * 8.33e-7 * exp(-abs(b) / radians(8.0));
}

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    output.position.z = output.position.w * 0.999999;
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float3 look = normalize(-input.toEye);
    float3 sky = skyMap.SampleLevel(lightLinear, look, 0).rgb * lighting.environment.x * input.color.rgb;
    if (horizonColor.w > 0.0)
    {
        // The image's share rises smoothly from none at the horizon to all of it at the blend's
        // top, so neither end shows as an edge, and below the horizon the color stands alone. The
        // two are mixed as light.
        float above = degrees(asin(clamp(look.y, -1.0, 1.0)));
        float share = horizonBlend.x > 0.0 ? smoothstep(0.0, horizonBlend.x, above) : step(0.0, above);
        sky = lerp(srgbToLinear(horizonColor.rgb), sky, share);
    }
    if (sunDisc.w > 0.0 && look.y > 0.0)
    {
        // Within the disc's edge, smoothed over a pixel's change of the cosine so it does not
        // stair, and capped below half floats' last value, since a low exposure takes the sun past it.
        float along = dot(look, sunDisc.xyz);
        float edge = max(fwidth(along), 1e-7);
        float within = smoothstep(sunDisc.w - edge, sunDisc.w + edge, along);
        sky = min(sky + sunLight.rgb * within * input.color.rgb, 60000.0);
    }
    if (moonDisc.w > 0.0 && look.y > 0.0)
    {
        // The moon's face at the pixel, a point on its sphere seen from here, lit where it faces
        // the sun and by the earthshine where it does not, the terminator smoothed over a hundredth
        // of the sphere's radius.
        float along = dot(look, moonDisc.xyz);
        float edge = max(fwidth(along), 1e-7);
        float within = smoothstep(moonDisc.w - edge, moonDisc.w + edge, along);
        if (within > 0.0)
        {
            float radius = sqrt(max(1.0 - moonDisc.w * moonDisc.w, 1e-12));
            float3 across = (look - moonDisc.xyz * along) / radius;
            float3 face = across - moonDisc.xyz * sqrt(max(1.0 - dot(across, across), 0.0));
            float lit = smoothstep(-0.01, 0.01, dot(face, normalize(moonSun.xyz)));
            sky = min(sky + (moonLight.rgb * lit + moonLight.w * (1.0 - lit)) * within * input.color.rgb, 60000.0);
        }
    }
    if (nightSky.z > 0.0 && look.y > 0.0)
    {
        // The stars and the band dimmed by the air along the way, its depth at the zenith over the
        // sine of the height, held at twenty times the zenith's near the horizon.
        float3 through = exp(-nightAir.rgb * min(1.0 / max(look.y, 1e-3), 20.0));
        float3 e = toStars(look, nightSky.x, nightSky.y);
        // The pixel's angle across, the longer of its two steps. The length of fwidth adds both
        // steps in each of the way's components, 1.4 to 1.9 pixels by how the view turns, and
        // would spread a star over two to nearly four times the pixels it is given.
        float pixel = max(length(ddx(look)), length(ddy(look)));
        float3 night = starLight(e, pixel, int(nightSky.w), uint(nightAir.w)) + galaxyLight(e);
        sky = min(sky + night * through * nightSky.z * input.color.rgb, 60000.0);
    }
    return float4(toDisplay(sky), 1.0);
}
