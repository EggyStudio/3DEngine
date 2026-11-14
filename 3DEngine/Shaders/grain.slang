// The turn the soft shadow's taps take at a point, the same for one world point against any origin.
module grain;

// The turn for a place against an origin: the place's cell of a 256th of a unit counted from the
// origin plus the origin's own cells, both whole, so the sum holds no float of the world's size,
// wrapped to 65,536 cells a side, which repeats the grain every 256 units, and mixed as integers.
// The origin is a whole count of 16 units, which its remainder past 256 holds exactly.
public float grainTurn(float3 place, float3 origin)
{
    int3 cells = int3(floor(place * 256.0)) + int3(fmod(origin, 256.0) * 256.0);
    uint3 c = uint3(cells) & 0xFFFFu;
    uint h = (c.x * 0x8DA6B343u) ^ (c.y * 0xD8163841u) ^ (c.z * 0xCB1AB31Fu);
    h ^= h >> 15;
    h *= 0x2C1B3C6Du;
    h ^= h >> 12;
    h *= 0x297A2D39u;
    h ^= h >> 15;
    return float(h) * (6.2831853 / 4294967296.0);
}
