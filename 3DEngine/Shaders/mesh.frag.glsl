#version 450

// Fallback fragment shader: emits opaque white so meshes are visible
// when no MaterialX-generated pipeline is bound (missing material,
// codegen failure, or compile failure). The vertex stage still writes
// `fragColor` at location 0.

layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(1.0, 1.0, 1.0, 1.0);
}
