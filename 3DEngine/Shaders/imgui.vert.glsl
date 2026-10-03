#version 450

layout(push_constant) uniform PushConstants {
    mat4 uProjection;
} pc;

layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;
layout(location = 2) in vec4 aColor;

layout(location = 0) out vec2 vUV;
layout(location = 1) out vec4 vColor;

void main()
{
    vUV = aUV;
    vColor = aColor;
    gl_Position = pc.uProjection * vec4(aPos, 0.0, 1.0);
}

