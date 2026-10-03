#version 450

layout(set = 0, binding = 0) uniform CameraUniform
{
    mat4 View;
    mat4 Projection;
} uCamera;

layout(push_constant) uniform PushConstants
{
    mat4 Model;
    vec4 Albedo;
} uPush;

layout(location = 0) in vec3 inPosition;

layout(location = 0) out vec4 fragColor;

void main()
{
    gl_Position = uCamera.Projection * uCamera.View * uPush.Model * vec4(inPosition, 1.0);
    fragColor = uPush.Albedo;
}

