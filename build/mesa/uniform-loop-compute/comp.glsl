#version 460
// Every other invocation, by its index, runs a loop whose condition reads a uniform buffer and whose
// body reads it, then reads the buffer after the loop and writes what it read. lavapipe crashes
// running it.
layout(local_size_x = 64) in;
struct Lamp { vec4 position; vec4 direction; vec4 color; };
layout(set = 0, binding = 4) uniform Lights { vec4 counts; Lamp lamps[16]; };
layout(set = 0, binding = 5, rgba16f) uniform writeonly image3D written;

void main()
{
    uint x = gl_GlobalInvocationID.x;
    if (x % 2u == 1u)
    {
        int index = 0;
        // counts.x is 1, so the body never runs.
        while (8 < int(counts.x))
            index = int(lamps[8].position.x);
        imageStore(written, ivec3(x % 16u, 0, 0), vec4(lamps[index].position.xyz, 0.0));
    }
}
