shader_type canvas_item;
render_mode blend_mix;

// 实例位置纹理 — 通过 RenderingDevice 管理（R32G32_SFLOAT）
uniform sampler2D instance_positions : repeat_disable;
uniform int position_texture_width = 16384;

void vertex() {
	// [INSTANCE_ID-mapping] 每个实例占一个 R32G32_SFLOAT texel。把线性 INSTANCE_ID
	// 映射为二维纹理的列/行后精确读取 mip 0，不使用过滤采样，pos.xy 就是像素位置。
    int row = INSTANCE_ID / position_texture_width;
    int column = INSTANCE_ID - row * position_texture_width;
    vec4 pos = texelFetch(instance_positions, ivec2(column, row), 0);
    VERTEX += pos.xy;
}

void fragment() {
    COLOR = texture(TEXTURE, UV);
}
