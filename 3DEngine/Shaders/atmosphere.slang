// The air the day sky is drawn through, read from the Atmosphere module's tables of a made sky as
// the module's README gives the march (SkyLight.Tables), so the sky drawn here is the luminance the
// module gives, which AirSkyTests reads. The tables come in one buffer of float4s, as AirTables
// lays them out, a head of four, then ten for each lane, then each table's cells, a float4 a lane
// to each cell, then each lane's row of the ground's light.

// The way toward the sun or the moon and the eye's altitude, what the module's luminance is
// multiplied by for the frame's light, a moonless sky's own glow, and the image's size.
public struct AirPush
{
    // xyz: the way toward the light, +Y up. w: the eye's altitude in meters.
    public float4 sun;
    // rgb: the frame's light outside the air over the module's sun's, times what a luminance is
    // multiplied by for the frame. w: unused.
    public float4 scale;
    // rgb: a moonless sky's own luminance in the same light, which the drawn sky adds under what
    // the air scatters. w: unused.
    public float4 night;
    // xy: the width and height of the image the pass writes. zw: unused.
    public uint4 size;
}

public static const float Pi = 3.14159265;

// The head's numbers. The planet's radius, the air's top and the two scale heights.
public float4 airHead(StructuredBuffer<float4> tables) { return tables[0]; }
// The aerosols' asymmetry, the ozone layer's bottom and depth, and the lowest height the ground's
// table holds.
public float4 airShapes(StructuredBuffer<float4> tables) { return tables[1]; }
// The steps of a march, and the transmittance's and the scattering's rows and columns.
public float4 airSizes(StructuredBuffer<float4> tables) { return tables[2]; }
// The scattering's columns, the lanes, and where the two tables' cells start.
public float4 airPlaces(StructuredBuffer<float4> tables) { return tables[3]; }

// A lane's numbers, ten float4s, the sun outside the air in its bands, the molecules' scattering,
// the aerosols' extinction and albedo, the ozone's absorption, the ground's albedo, the three rows
// of its matrix to linear sRGB, and where its row of the ground's light starts and how long it is.
public float4 laneAt(StructuredBuffer<float4> tables, uint lane, uint part) { return tables[4 + lane * 10 + part]; }

// A table's three bands of a lane read bilinearly at a row and a column given as fractions of
// their cells, the next row and column held at the last, first along the column and then the row.
public float3 tableAt(StructuredBuffer<float4> tables, uint start, uint rows, uint columns, uint lanes, uint lane, float row, float column)
{
    float r = clamp(row, 0.0, float(rows - 1)), c = clamp(column, 0.0, float(columns - 1));
    uint r0 = uint(r), c0 = uint(c);
    uint r1 = min(r0 + 1, rows - 1), c1 = min(c0 + 1, columns - 1);
    float fr = r - float(r0), fc = c - float(c0);
    float3 a = lerp(tables[start + (r0 * columns + c0) * lanes + lane].rgb, tables[start + (r0 * columns + c1) * lanes + lane].rgb, fc);
    float3 b = lerp(tables[start + (r1 * columns + c0) * lanes + lane].rgb, tables[start + (r1 * columns + c1) * lanes + lane].rgb, fc);
    return lerp(a, b, fr);
}

// Where an altitude falls among a table's rows, crowded near the ground as the square of their
// row, and where a cosine falls among its columns.
public float rowOf(float altitude, float top, uint rows) { return sqrt(saturate(altitude / top)) * float(rows - 1); }
public float columnOf(float cosine, uint columns) { return (clamp(cosine, -1.0, 1.0) + 1.0) * 0.5 * float(columns - 1); }

// The sky's light on a level surface at the ground in a lane's three bands, from the light's
// height in degrees over the horizon, read between the two heights about it in its logarithm,
// and none under the lowest the table holds.
public float3 groundAt(StructuredBuffer<float4> tables, uint lane, float degrees)
{
    float4 row = laneAt(tables, lane, 9);
    uint start = uint(row.x), count = uint(row.y);
    if (count == 0 || degrees < airShapes(tables).w)
        return 0.0;
    if (degrees <= tables[start].w)
        return exp(tables[start].rgb);
    if (degrees >= tables[start + count - 1].w)
        return exp(tables[start + count - 1].rgb);
    uint low = 0, high = count - 1;
    while (high - low > 1)
    {
        uint middle = (low + high) / 2;
        if (tables[start + middle].w <= degrees) low = middle; else high = middle;
    }
    float4 a = tables[start + low], b = tables[start + high];
    return exp(lerp(a.rgb, b.rgb, (degrees - a.w) / max(b.w - a.w, 1e-6)));
}

// How far a way from an altitude whose cosine with the zenith is m runs to the ground, or -1 where
// it misses it, and to the air's top, each root's argument written so a float keeps it, r^2 m^2
// less h (2R + h) inward and r^2 m^2 plus (top - h) (2R + h + top) outward.
public float2 distances(float radius, float top, float altitude, float m)
{
    float h = max(altitude, 0.0);
    float r = radius + h;
    float rm2 = r * r * m * m;
    float inward = rm2 - h * (2.0 * radius + h);
    float outward = rm2 + (top - h) * (2.0 * radius + h + top);
    float toGround = m < 0.0 && inward >= 0.0 ? max(0.0, -r * m - sqrt(inward)) : -1.0;
    float toTop = outward > 0.0 ? max(0.0, -r * m + sqrt(outward)) : 0.0;
    return float2(toGround, toTop);
}

// The integral of the transmittance over a step, (1 - e^(-x l)) / x, or the length where the air
// takes nearly nothing.
public float3 span(float3 extinction, float length)
{
    float3 t = extinction * length;
    return select(t < 1e-5, length, (1.0 - exp(-t)) / max(extinction, 1e-30));
}

// The molecules' phase function, 3 / (16 pi) (1 + cos^2).
public float rayleighPhase(float c) { return 3.0 / (16.0 * Pi) * (1.0 + c * c); }

// Cornette and Shanks's phase function for the aerosols.
public float aerosolPhase(float g, float c)
{
    return 3.0 / (8.0 * Pi) * (1.0 - g * g) * (1.0 + c * c) / ((2.0 + g * g) * pow(max(1.0 + g * g - 2.0 * g * c, 1e-6), 1.5));
}

// The sky's luminance in linear sRGB, candelas a square meter in the module's sun outside the air,
// along a unit way from an altitude, the light toward a unit way, as SkyLight.Luminance gives it,
// the sun's disc left out.
public float3 airLuminance(StructuredBuffer<float4> tables, float3 view, float3 toSun, float altitude)
{
    float4 head = airHead(tables), shapes = airShapes(tables), sizes = airSizes(tables), places = airPlaces(tables);
    float radius = head.x, top = head.y;
    uint steps = uint(sizes.x), transmittanceRows = uint(sizes.y), transmittanceColumns = uint(sizes.z), scatterRows = uint(sizes.w);
    uint scatterColumns = uint(places.x), lanes = uint(places.y), transmittances = uint(places.z), scatters = uint(places.w);

    float2 d = distances(radius, top, altitude, view.y);
    bool ground = d.x >= 0.0;
    float distance = ground ? d.x : d.y;
    float c = dot(view, toSun);
    float rayleighShare = rayleighPhase(c), aerosolShare = aerosolPhase(shapes.x, c);
    // Each lane's light and the share come through so far, set a lane at a time, since an array
    // of vectors given scalars takes them a component at a time.
    float3 light[4], through[4];
    [unroll]
    for (uint lane = 0; lane < 4; lane++)
    {
        light[lane] = 0.0;
        through[lane] = 1.0;
    }
    float3 start = float3(0.0, radius + max(altitude, 0.0), 0.0);
    for (uint i = 0; i < steps; i++)
    {
        // The steps lengthen as the square of their count, from the eye out, each read at
        // its middle.
        float from = float(i) / float(steps), to = float(i + 1) / float(steps);
        float near = from * from * distance, far = to * to * distance;
        float stride = far - near;
        float3 p = start + view * ((near + far) * 0.5);
        float at = length(p);
        float y = at - radius, held = max(y, 0.0);
        float u = dot(p / at, toSun);
        float molecules = exp(-held / head.z), aerosols = exp(-held / head.w);
        float ozone = max(0.0, 1.0 - abs(2.0 * (y - shapes.y) / shapes.z - 1.0));
        float transmittanceRow = rowOf(y, top, transmittanceRows), scatterRow = rowOf(y, top, scatterRows);
        float transmittanceColumn = columnOf(u, transmittanceColumns), scatterColumn = columnOf(u, scatterColumns);
        [unroll]
        for (uint lane = 0; lane < 4; lane++)
        {
            if (lane >= lanes) break;
            float3 rayleigh = laneAt(tables, lane, 1).rgb * molecules;
            float3 extinguished = laneAt(tables, lane, 2).rgb * aerosols;
            float3 scattered = extinguished * laneAt(tables, lane, 3).rgb;
            float3 extinction = rayleigh + extinguished + laneAt(tables, lane, 4).rgb * ozone;
            float3 toward = tableAt(tables, transmittances, transmittanceRows, transmittanceColumns, lanes, lane, transmittanceRow, transmittanceColumn);
            float3 more = exp(tableAt(tables, scatters, scatterRows, scatterColumns, lanes, lane, scatterRow, scatterColumn));
            float3 source = (rayleigh * rayleighShare + scattered * aerosolShare) * toward + (rayleigh + scattered) * more;
            light[lane] += through[lane] * source * span(extinction, stride);
            through[lane] *= exp(-extinction * stride);
        }
    }
    float u = 0.0;
    if (ground)
    {
        float3 p = start + view * d.x;
        u = dot(normalize(p), toSun);
    }
    float3 luminance = 0.0;
    [unroll]
    for (uint lane = 0; lane < 4; lane++)
    {
        if (lane >= lanes) break;
        float3 albedo = laneAt(tables, lane, 5).rgb;
        float3 lit = light[lane];
        if (ground)
            lit += through[lane] * tableAt(tables, transmittances, transmittanceRows, transmittanceColumns, lanes, lane, 0.0, columnOf(u, transmittanceColumns))
                * max(u, 0.0) * albedo / Pi;
        lit *= laneAt(tables, lane, 0).rgb;
        // The sky's light on the ground a way meets, from the light's height there in degrees.
        if (ground)
            lit += through[lane] * albedo / Pi * groundAt(tables, lane, degrees(asin(clamp(u, -1.0, 1.0))));
        luminance += float3(dot(laneAt(tables, lane, 6).rgb, lit), dot(laneAt(tables, lane, 7).rgb, lit), dot(laneAt(tables, lane, 8).rgb, lit));
    }
    return luminance;
}
