#version 460
// A triangle over the middle of the view, from the vertex's index alone, whose edges cross the
// groups of pixels lavapipe shades together, and the way the fragment stage's ray is cast from each
// point of it, so the rays of pixels along the scene's triangle's long edge meet it and their
// neighbors' miss.
layout(location = 3) out vec3 way;

void main()
{
    vec2 corner = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4((corner * 2.0 - 1.0) * 0.4, 0.0, 1.0);
    way = vec3(corner * 2.0 - 1.0, 1.0);
}
