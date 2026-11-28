// The engine's hand to the RadianceCascades module's split cascades (rc_split.slang) over its
// buffers, the store's uints changed atomically and its light kept, which the split passes
// (gi_split.slang) and the tests' kernels over the store take alike, each binding its own buffers.
module gi_split_store;

__exported import rc_split;

// The store's uints over a buffer of atomics, which the module asks of an engine for every target
// it compiles to, where a plain buffer's uint taken atomically compiles for SPIR-V alone.
public struct StoreUints : IStoreUints
{
    public RWStructuredBuffer<Atomic<uint>> uints;

    public uint load(uint index) { return uints[index].load(); }
    public void store(uint index, uint value) { uints[index].store(value); }
    public uint add(uint index, uint value) { return uints[index].add(value); }
    public uint compareExchange(uint index, uint expected, uint desired) { return uints[index].compareExchange(expected, desired); }
}

// The store's light over a buffer of float4, each probe's own along its ways and the merged after.
public struct StoreLight : IStoreLight
{
    public RWStructuredBuffer<float4> light;

    public float4 lightAt(uint index) { return light[index]; }
    public void keepLight(uint index, float4 value) { light[index] = value; }
}

// The store's light over two buffers of float4, the probes' own along their ways in the first and
// the merged in the second, the light laid out as splitLightAt lays it with the merged past the
// own, so each buffer holds half the light and binds within a range the whole may be larger than.
public struct StoreLightHalves : IStoreLight
{
    public RWStructuredBuffer<float4> own;
    public RWStructuredBuffer<float4> merged;
    // The float4 of the probes' own light, splitLightFirst over every cascade.
    public uint first;

    public float4 lightAt(uint index) { return index < first ? own[index] : merged[index - first]; }

    public void keepLight(uint index, float4 value)
    {
        if (index < first)
            own[index] = value;
        else
            merged[index - first] = value;
    }
}
