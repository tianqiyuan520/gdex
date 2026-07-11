#[vertex]
#version 460

// 顶点输入：pos.xy + uv.xy
layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_uv;

layout(location = 0) out vec2 out_uv;

// 实例位置存储缓冲（每实例一个 vec2）
layout(set = 0, binding = 0) readonly buffer InstanceData {
    vec2 positions[];
} instance_buffer;

void main() {
    // 读取当前实例的位置
    vec2 instance_pos = instance_buffer.positions[gl_InstanceIndex];
    
    // 输出顶点位置（在 clip space 中：-1..1）
    // 将像素位置映射到 NDC
    vec2 pos = in_position + instance_pos;
    gl_Position = vec4(pos.x / 640.0 - 1.0, -(pos.y / 360.0 - 1.0), 0.0, 1.0);
    
    out_uv = in_uv;
}

#[fragment]
#version 460

layout(location = 0) in vec2 in_uv;
layout(location = 0) out vec4 out_color;

layout(set = 1, binding = 0) uniform sampler2D texture_sampler;

void main() {
    out_color = texture(texture_sampler, in_uv);
}