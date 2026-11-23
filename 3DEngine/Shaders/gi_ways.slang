// The ways the screen's gather sends its rays along, around +z, the hemisphere's square unfolded
// from the octahedron's upper half, the gather by pixel's ways drawn by the cosine, and those its
// guide draws toward the light the world's probes hold (gi_screen).
module gi_ways;

import gi;

// The cells along each side of the guide's table, the hemisphere's square around the normal.
public static const int GuideCells = 8;

// A direction of the hemisphere around +z for a point of the square, the octahedron's upper half
// unfolded onto it, its length before it was made one in w.
public float4 hemisphere(float2 uv)
{
    float2 a = uv * 2.0 - 1.0;
    float x = (a.x + a.y) * 0.5, y = (a.x - a.y) * 0.5;
    float3 d = float3(x, y, 1.0 - abs(x) - abs(y));
    return float4(normalize(d), length(d));
}

// A way of the hemisphere around +z for the texel at id, as likely as the cosine of its angle to
// +z. Each four by four block of texels holds the sixteen strata of the square once, so a blend
// over the block takes the ways a probe's sixteen rays would, each texel at a place in its stratum,
// the same from frame to frame, so a still view holds still with no history. The order the block's
// texels take the strata in and each place are drawn from the block's and the texel's own bits
// (scattered), so the texels that take one way lie apart across the picture with no line among
// them.
public float3 cosineWay(uint2 id)
{
    return cosineOf(strataPoint(id));
}

// The way of the hemisphere around +z as likely as its cosine for a point of the square, its
// distance from +z squared by x and its turn by y.
float3 cosineOf(float2 square)
{
    float r = sqrt(square.x), turn = 2.0 * Pi * square.y;
    return float3(r * cos(turn), r * sin(turn), sqrt(max(1.0 - square.x, 0.0)));
}

// Two numbers whose every bit hangs on every bit of the two given, each taken through a
// multiply and add twice with the other mixed in and its high half folded onto its low, so texels
// side by side draw nothing alike.
uint2 scattered(uint2 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    return v;
}

// A place in a stratum, each of its two numbers from 0 to under 1, from the bits of the texel.
float2 placeIn(uint2 id)
{
    return float2(scattered(id) >> 8u) / 16777216.0;
}

// The point of the square the texel at id draws its way from, in its stratum (cosineWay). The
// block's own bits turn the order its texels take the strata in, each texel's index in the block
// flipped by them alike, so the block still holds each stratum once.
public float2 strataPoint(uint2 id)
{
    uint flip = scattered((id >> 2u) ^ uint2(0x68E31DA4u, 0xB5297A4Du)).x & 15u;
    uint stratum = TracePlaces[((id.x & 3u) + 4u * (id.y & 3u)) ^ flip];
    return (float2(stratum & 3u, stratum >> 2u) + placeIn(id)) / 4.0;
}

// The strata of the square two across and four up in the order the eight texels of a four by four
// block that draw alike take them (guidePoint).
static const uint GuidePlaces[8] = { 0, 5, 3, 6, 4, 1, 7, 2 };

// The point of the square the texel at id draws its guided way from. The eight texels of each four
// by four block that draw by the cosine and the eight that draw from the table each take the eight
// strata two across and four up once, so each draw covers the whole square within the block, where
// the sixteen strata of strataPoint split by the checkerboard leave each color two rows of them.
float2 guidePoint(uint2 id)
{
    uint flip = (scattered((id >> 2u) ^ uint2(0x68E31DA4u, 0xB5297A4Du)).y >> (3u * ((id.x + id.y) & 1u))) & 7u;
    uint stratum = GuidePlaces[(((id.x & 3u) >> 1u) + 2u * (id.y & 3u)) ^ flip];
    return (float2(stratum & 1u, stratum >> 1u) + placeIn(id)) / float2(2.0, 4.0);
}

// The gather by pixel's way at id where the guide's table holds light, drawn by the cosine at every
// other texel and from the table's cells in proportion to their weights at the rest, uniformly in
// the cell drawn, with the ray's weight, its cosine over pi times the chance of its way under the
// two draws mixed half and half, so the mean of the texels' light stays the irradiance. A way in a
// cell spans two over the cube of its octahedron point's distance of the hemisphere for each unit
// of the square (hemisphere), so the table's draw has a chance of the cell's share of the weights
// times the cells over two times that cube for each unit of the sphere.
public float3 guidedWay(uint2 id, float cells[GuideCells * GuideCells], float total, out float weight)
{
    float2 square = guidePoint(id);
    float3 local;
    float apart;
    int at;
    if (((id.x + id.y) & 1u) == 0u)
    {
        local = cosineOf(square);
        float3 d = local / (abs(local.x) + abs(local.y) + abs(local.z));
        int2 cell = min(int2((float2(d.x + d.y, d.x - d.y) * 0.5 + 0.5) * float(GuideCells)), int2(GuideCells - 1));
        at = cell.x + cell.y * GuideCells;
        apart = length(d);
    }
    else
    {
        // A row by the rows' sums, then a cell of it by the cells' weights, the rest of each
        // pick placing the way within the cell.
        float pick = square.y * total;
        int row = 0;
        float across = 0.0;
        for (int r = 0; r < GuideCells; r++)
        {
            float sum = 0.0;
            for (int c = 0; c < GuideCells; c++)
                sum += cells[c + r * GuideCells];
            across = sum;
            row = r;
            if (pick < sum || r == GuideCells - 1)
                break;
            pick -= sum;
        }
        float down = saturate(pick / max(across, 1e-30));
        pick = square.x * across;
        int column = 0;
        for (; column < GuideCells - 1 && pick >= cells[column + row * GuideCells]; column++)
            pick -= cells[column + row * GuideCells];
        at = column + row * GuideCells;
        float within = saturate(pick / max(cells[at], 1e-30));
        float4 drawn = hemisphere((float2(column, row) + float2(within, down)) / float(GuideCells));
        local = drawn.xyz;
        apart = drawn.w;
    }
    float chance = 0.5 * local.z / Pi + 0.25 * float(GuideCells * GuideCells) * cells[at] / total * apart * apart * apart;
    weight = local.z / (Pi * max(chance, 1e-12));
    return local;
}
