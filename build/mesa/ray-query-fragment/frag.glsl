#version 460
#extension GL_EXT_ray_query : require
// One ray query from (0, 0, -1) along the direction the vertex stage gives, and where it meets a
// triangle, a loop whose condition reads a uniform buffer and whose body reads it, then a read of
// the same buffer after the loop. lavapipe crashes drawing it where some of a group's pixels are
// outside the triangle drawn and some of its rays miss. Red where the ray met the scene's triangle,
// blue where it did not.
layout(set = 1, binding = 28) uniform accelerationStructureEXT scene;
layout(set = 1, binding = 22) uniform Lights { vec4 sun; vec4 counts; vec4 lamps[64]; };
layout(location = 3) in vec3 way;
layout(location = 0) out vec4 color;

void main()
{
    vec3 last = vec3(0.0);
    color = vec4(0.0, 0.0, 1.0, 1.0);
    rayQueryEXT query;
    rayQueryInitializeEXT(query, scene, gl_RayFlagsOpaqueEXT, 0xFF, vec3(0.0, 0.0, -1.0), 0.0, way, 10000.0);
    rayQueryProceedEXT(query);
    if (rayQueryGetIntersectionTypeEXT(query, true) == gl_RayQueryCommittedIntersectionTriangleEXT)
    {
        // counts.x is 1, so the loop's body never runs.
        while (5 < int(counts.x))
            last = lamps[20].xyz;
        color = vec4(sun.xyz * last + vec3(1.0, 0.0, 0.0), 1.0);
    }
}
